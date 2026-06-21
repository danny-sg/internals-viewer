using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay.Plans;

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

    public string SchemaName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// The plan_handle action captured with the event, identifying the compiled plan the
    /// event was raised under. Links an event to a single <see cref="Plans.ExecutionPlan"/>.
    /// </summary>
    public string PlanHandle { get; set; } = string.Empty;

    public virtual string Description => string.Empty;

    public PlanNodeIdentifier? PlanNodeIdentifier { get; set; }
}