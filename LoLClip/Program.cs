global using System.Net.Http;
global using System.Net.WebSockets;
global using System.Text;
global using System.Text.Json;
global using Microsoft.Extensions.Logging;


var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger("lol_obs_replay");

var httpHandler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
using var http = new HttpClient(httpHandler);

var customManager = new CustomEventManager();
var history = new List<JsonElement>();

var asyncs = new GameAsyncs();

// ハンドラ登録
customManager.Register("TeamFight", async _ =>
{
    logger.LogInformation("🔥 集団戦検出！");
    await asyncs.TriggerObsReplayAsync(logger);
});
customManager.Register("MyMultikill", async ev =>
{
    logger.LogInformation("🏆 自分のマルチキル！ {Event}", ev.ToString());
    await asyncs.TriggerObsReplayAsync(logger);
});
customManager.Register("MyDeath", async ev =>
{
    logger.LogInformation("💀 自分がデス… {Event}", ev.ToString());
    await asyncs.TriggerObsReplayAsync(logger);
});

while (true)
{
    var myName = await asyncs.GetSummonerNameAsync(logger, http);
    logger.LogInformation("自分のサモナーネーム: {MyName}", myName);
    history.Clear();
    customManager.Reset();

    // ゲーム進行中ループ
    while (true)
    {
        try
        {
            var resp = await http.GetAsync(asyncs.ALLGAMEDATA_URL);
            var str = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(str);
            history.Add(doc.RootElement.Clone());
            if (history.Count > 10) history.RemoveAt(0);
            await customManager.CheckAllAsync(history, myName, logger);
            if (doc.RootElement.TryGetProperty("gameData", out var gd) &&
                gd.TryGetProperty("gameEnded", out var ge) && ge.GetBoolean())
                break;
        }
        catch (Exception e)
        {
            logger.LogWarning("API取得失敗: {Error}", e.Message);
        }
        await Task.Delay(1000);
    }
    await asyncs.WaitForGameEndAsync(logger, http);
}


class CustomEventManager
{
    private readonly Dictionary<string, Func<JsonElement, Task>> _handlers = new();
    private int _lastEventId = -1;

    public void Reset() => _lastEventId = -1;

    public void Register(string eventName, Func<JsonElement, Task> handler)
        => _handlers[eventName] = handler;

    public async Task CheckAllAsync(List<JsonElement> history, string myName, ILogger logger)
    {
        // 集団戦（死亡2人以上）
        if (DetectTeamfight(history))
        {
            if (_handlers.TryGetValue("TeamFight", out var handler))
                await handler(JsonSerializer.Deserialize<JsonElement>("{\"history\": []}"));
        }

        // 通常イベント
        if (history.Count == 0) return;
        var last = history[^1];
        if (last.TryGetProperty("events", out var eventsProp) &&
            eventsProp.TryGetProperty("Events", out var eventsArr) &&
            eventsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var ev in eventsArr.EnumerateArray())
            {
                if (!ev.TryGetProperty("EventID", out var idProp))
                    continue;
                var id = idProp.GetInt32();
                if (id <= _lastEventId)
                    continue;
                _lastEventId = id;

                if (ev.TryGetProperty("EventName", out var nameProp))
                {
                    var eventName = nameProp.GetString();
                    if (eventName == "Multikill" && ev.GetProperty("KillerName").GetString() == myName)
                    {
                        if (_handlers.TryGetValue("MyMultikill", out var handler))
                            await handler(ev);
                    }
                    else if (eventName == "ChampionDeath" && ev.GetProperty("VictimName").GetString() == myName)
                    {
                        if (_handlers.TryGetValue("MyDeath", out var handler))
                            await handler(ev);
                    }
                }
            }
        }
    }

    private bool DetectTeamfight(List<JsonElement> history)
    {
        if (history.Count == 0) return false;
        var last = history[^1];
        if (!last.TryGetProperty("allPlayers", out var players) || players.ValueKind != JsonValueKind.Array)
            return false;
        int deaths = 0;
        foreach (var p in players.EnumerateArray())
        {
            if (p.TryGetProperty("isDead", out var isDead) && isDead.GetBoolean())
                deaths++;
        }
        return deaths >= 2;
    }
}

class GameAsyncs
{
    public string ALLGAMEDATA_URL = "https://127.0.0.1:2999/liveclientdata/allgamedata";
    public string OBS_WS_URL = "ws://localhost:4455";
    
    public async Task<string> GetSummonerNameAsync(ILogger logger, HttpClient http)
{
    logger.LogInformation("サモナーネーム自動取得: ゲーム開始待機中…");
    while (true)
    {
        try
        {
            var resp = await http.GetAsync(ALLGAMEDATA_URL);
            var str = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(str);
            if (doc.RootElement.TryGetProperty("activePlayer", out var ap) &&
                ap.TryGetProperty("summonerName", out var sn))
            {
                var name = sn.GetString();
                logger.LogInformation($"ゲーム開始検知！自分のサモナーネーム: {name}");
                return name;
            }
        }
        catch (Exception e)
        {
            logger.LogDebug("サモナーネーム取得リトライ: {Error}", e.Message);
        }
        await Task.Delay(2000);
    }
}

    public async Task WaitForGameEndAsync(ILogger logger, HttpClient http)
{
    logger.LogInformation("ゲーム終了検知まで監視中…");
    while (true)
    {
        try
        {
            var resp = await http.GetAsync(ALLGAMEDATA_URL);
            var str = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(str);
            if (doc.RootElement.TryGetProperty("gameData", out var gd) &&
                gd.TryGetProperty("gameEnded", out var ge) && ge.GetBoolean())
            {
                logger.LogInformation("ゲーム終了検知！待ち受けに戻ります。");
                break;
            }
        }
        catch (Exception e)
        {
            logger.LogWarning("ゲーム終了監視中のAPIエラー: {Error}", e.Message);
        }
        await Task.Delay(2000);
    }
}

    public async Task TriggerObsReplayAsync(ILogger logger)
{
    try
    {
        using var ws = new ClientWebSocket();
        var uri = new Uri(OBS_WS_URL);
        await ws.ConnectAsync(uri, CancellationToken.None);
        var payload = JsonSerializer.Serialize(new
        {
            op = 6,
            d = new
            {
                requestType = "SaveReplayBuffer",
                requestId = "saveReplay"
            }
        });
        var bytes = Encoding.UTF8.GetBytes(payload);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        var resp = Encoding.UTF8.GetString(buffer, 0, result.Count);
        logger.LogInformation("Replay triggered! OBS response: {Resp}", resp);
    }
    catch (Exception e)
    {
        logger.LogError("OBS連携失敗: {Error}", e.Message);
    }
}
}
