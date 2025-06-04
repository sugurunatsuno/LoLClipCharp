using System.IO;

var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText("config.json")) ?? new AppConfig();

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

var customManager = new CustomEventManager(config);
var history = new List<JsonElement>();

var gameService = new GameService(config);

// ハンドラ登録
customManager.Register("TeamFight", async _ =>
{
    logger.LogInformation("🔥 集団戦検出！");
    await gameService.TriggerObsReplayAsync(logger);
});
customManager.Register("MyMultikill", async ev =>
{
    logger.LogInformation("🏆 自分のマルチキル！ {Event}", ev.ToString());
    await gameService.TriggerObsReplayAsync(logger);
});
customManager.Register("MyDeath", async ev =>
{
    logger.LogInformation("💀 自分がデス… {Event}", ev.ToString());
    await gameService.TriggerObsReplayAsync(logger);
});

while (true)
{
    var myName = await gameService.GetSummonerNameAsync(logger, http);
    logger.LogInformation("自分のサモナーネーム: {MyName}", myName);

    // ゲーム進行中ループ
    while (true)
    {
        try
        {
            var resp = await http.GetAsync(gameService.ALLGAMEDATA_URL);
            var str = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(str);
            history.Add(doc.RootElement.Clone());
            if (history.Count > config.HistoryLimit) history.RemoveAt(0);
            await customManager.CheckAllAsync(history, myName, logger);
            if (doc.RootElement.TryGetProperty("gameData", out var gd) &&
                gd.TryGetProperty("gameEnded", out var ge) && ge.GetBoolean())
                break;
        }
        catch (Exception e)
        {
            logger.LogWarning("API取得失敗: {Error}", e.Message);
        }
        await Task.Delay(config.MainLoopDelayMs);
    }
    await gameService.WaitForGameEndAsync(logger, http);
}
