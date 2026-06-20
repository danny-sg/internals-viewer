namespace InternalsViewer.Replay.Events;

public record EventResult
{
    public required string Name { get; set; }

    public DateTime Timestamp { get; set; }

    public int DatabaseId { get; set; }

    public Dictionary<string, object> Data { get; set; } = new();

    public Dictionary<string, object> Actions { get; set; } = new();
}

public static class EventResultExtensions
{
    public static short? GetShort(this EventResult e, string key)
    {
        if (e.Data.TryGetValue(key, out var val) && short.TryParse(val?.ToString(), out var i))
        {
            return i;
        }

        return null;
    }

    public static int? GetInt(this EventResult e, string key)
    {
        if (e.Data.TryGetValue(key, out var val) && int.TryParse(val?.ToString(), out var i))
        {
            return i;
        }

        return null;
    }

    public static long? GetLong(this EventResult e, string key)
    {
        if (e.Data.TryGetValue(key, out var val) && long.TryParse(val?.ToString(), out var i))
        {
            return i;
        }

        return null;
    }

    public static ulong? GetUlong(this EventResult e, string key)
    {
        if (e.Data.TryGetValue(key, out var val) && ulong.TryParse(val?.ToString(), out var i))
        {
            return i;
        }

        return null;
    }

    public static string GetString(this EventResult e, string key)
    {
        return e.Data.TryGetValue(key, out var val) ? val?.ToString() ?? string.Empty : string.Empty;
    }

    public static int GetDatabaseId(this EventResult e)
    {
        if (e.Actions.TryGetValue("database_id", out var val) && int.TryParse(val?.ToString(), out var i))
        {
            return i;
        }

        return -1;
    }
}

public record EngineEvent
{
    public int DatabaseId { get; set; }

    public DateTime Timestamp { get; set; }

    public string Name { get; set; } = string.Empty;

    public double TimeTicks { get; set; }

    public double TimeMs { get; set; }

    public short FileId { get; set; }

    public int PageId { get; set; }
}

public sealed record IoEvent : EngineEvent
{
    public bool IsRead { get; init; }
}


public sealed record PageEvent : EngineEvent
{
    public required string Type { get; init; }
}

public sealed record LockEvent : EngineEvent
{
    public string Mode { get; init; } = string.Empty;

    public string ResourceType { get; init; } = string.Empty;
}

public sealed record WaitEvent : EngineEvent
{
    public string WaitType { get; init; } = string.Empty;

    public long Duration { get; init; }
}
