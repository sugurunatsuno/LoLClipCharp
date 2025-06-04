public class AppConfig
{
    public string AllGameDataUrl { get; set; } = "";
    public string ObsWebSocketUrl { get; set; } = "";
    public int HistoryLimit { get; set; } = 10;
    public int MainLoopDelayMs { get; set; } = 1000;
    public int GameStartDelayMs { get; set; } = 2000;
    public int GameEndDelayMs { get; set; } = 2000;
    public int TeamfightDeathThreshold { get; set; } = 2;
    public int ObsBufferSize { get; set; } = 4096;
}
