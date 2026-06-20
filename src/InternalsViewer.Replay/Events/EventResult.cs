using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay.Locks;

namespace InternalsViewer.Replay.Events;

public record EventResult
{
    public int SequenceId { get; set; }

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

    public int SequenceId { get; set; }

    public DateTime Timestamp { get; set; }

    public string Name { get; set; } = string.Empty;

    public double TimeTicks { get; set; }

    public double TimeMs { get; set; }

    public long Duration { get; set; }

    public PageAddress? PageAddress { get; set; }

    public int ObjectId { get; set; }

    public string ObjectName { get; set; } = string.Empty;

    public virtual string Description => string.Empty;
}

public sealed record IoEvent : EngineEvent
{
    public bool IsRead { get; init; }

    public override string Description => $"{(IsRead ? "Read" : "Write")}";
}


public sealed record PageEvent : EngineEvent
{
    public required string Type { get; init; }

    public override string Description => Type;
}

public sealed record LockEvent : EngineEvent
{
    public LockMode LockMode { get; init; }

    public LockResourceType ResourceType { get; init; }

    public RowIdentifier? RowIdentifier { get; set; }

    public string? KeyHash { get; set; }

    public override string Description => $"{LockMode}/{ResourceType}";
}

public sealed record WaitEvent : EngineEvent
{
    public WaitType WaitType { get; set; }

    public override string Description => $"{WaitType}";
}
