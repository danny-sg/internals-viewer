using System.Drawing;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay.Plans;

namespace InternalsViewer.Replay.Events.EventTypes;

public record EngineEvent
{
    public int DatabaseId { get; set; }

    public int SequenceId { get; set; }

    public DateTime Timestamp { get; set; }

    public string Name { get; set; } = string.Empty;

    public long TimeMs { get; set; }

    public long Duration { get; set; }

    public PageAddress? PageAddress { get; set; }

    public int ObjectId { get; set; }

    public string ObjectName { get; set; } = string.Empty;

    internal string SchemaName { get; set; } = string.Empty;

    internal string TableName { get; set; } = string.Empty;

    internal string IndexName { get; set; } = string.Empty;

    internal string PlanHandle { get; set; } = string.Empty;

    public virtual string Description => string.Empty;

    public PlanNodeIdentifier? PlanNodeIdentifier { get; set; }

    public Color DisplayColour { get; set; }
}
