using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Plans;

namespace InternalsViewer.Query.Events.EventTypes;

public record EngineEvent
{
    public int DatabaseId { get; set; }

    public int SequenceId { get; set; }

    public DateTime Timestamp { get; set; }

    public string Name { get; set; } = string.Empty;

    public long TimeUs { get; set; }

    public long DurationUs { get; set; }

    public PageAddress? PageAddress { get; set; }

    public int ObjectId { get; set; }

    public string ObjectName { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Interned id of the plan handle this event belongs to (see <see cref="PlanHandleRegistry"/>), used
    /// only to link the event to its execution plan. <see cref="PlanHandleRegistry.None"/> when the event
    /// carries no plan handle.
    /// </summary>
    internal short PlanHandleId { get; set; }

    public int ThreadId { get; set; }

    public EventCategory? Category { get; set; }

    public virtual string Description => string.Empty;

    public PlanNodeIdentifier? PlanNodeIdentifier { get; set; }
}
