class CustomEventManager
{
    private readonly AppConfig _config;
    private readonly Dictionary<string, Func<JsonElement, Task>> _handlers = new();

    public CustomEventManager(AppConfig config)
    {
        _config = config;
    }

    public void Register(string eventName, Func<JsonElement, Task> handler)
        => _handlers[eventName] = handler;

    public async Task CheckAllAsync(List<JsonElement> history, string myName, ILogger logger)
    {
        // 集団戦（死亡N人以上）
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
        return deaths >= _config.TeamfightDeathThreshold;
    }
}
