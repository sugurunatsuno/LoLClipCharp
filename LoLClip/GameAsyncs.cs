class GameAsyncs
{
    private readonly AppConfig _config;

    public GameAsyncs(AppConfig config)
    {
        _config = config;
    }

    public string ALLGAMEDATA_URL => _config.AllGameDataUrl;
    public string OBS_WS_URL => _config.ObsWebSocketUrl;

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
                    return name!;
                }
            }
            catch (Exception e)
            {
                logger.LogDebug("サモナーネーム取得リトライ: {Error}", e.Message);
            }
            await Task.Delay(_config.GameStartDelayMs);
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
            await Task.Delay(_config.GameEndDelayMs);
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
            var buffer = new byte[_config.ObsBufferSize];
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
