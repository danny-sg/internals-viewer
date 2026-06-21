using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Replay.Events;

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