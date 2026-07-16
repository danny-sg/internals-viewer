using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.Query.Events;

public record EngineEvent
{
    public int DatabaseId { get; set; }

    public int SequenceId { get; set; }

    public DateTime Timestamp { get; set; }

    public string Name { get; set; } = string.Empty;

    public virtual long TimeUs { get; set; }

    public virtual long DurationUs { get; set; }

    public AllocationUnit? AllocationUnit { get; set; }

    public virtual int ObjectId => AllocationUnit?.ObjectId ?? 0;

    public virtual string ObjectName => AllocationUnit?.DisplayName ?? string.Empty;

    public virtual string SchemaName => AllocationUnit?.SchemaName ?? string.Empty;

    public virtual string TableName => AllocationUnit?.TableName ?? string.Empty;

    public virtual string IndexName => AllocationUnit?.IndexName ?? string.Empty;

    internal short PlanHandleId { get; set; }

    public int ThreadId { get; set; }

    public EventCategory? Category { get; set; }

    public virtual string Description => string.Empty;

    public virtual string Detail => Description;

    public PlanNodeIdentifier? PlanNodeIdentifier { get; set; }

    public CallStackNode? CallStack { get; set; }

    /// <summary>
    /// The End event folded into this one, whose call stack is part of this event's work
    /// </summary>
    /// <remarks>
    /// <see cref="Consolidation.IntervalCollapser"/> keeps the Begin and drops the End, but the End carries its own
    /// frames (the release/completion path). Holding it here keeps those reachable from the event that survived, the
    /// same way an <see cref="Interfaces.Events.IEventGroup"/> owns the raw events it was built from.
    /// </remarks>
    public EngineEvent? FoldedFrom { get; set; }

    public ulong? TaskAddress { get; set; }

    public ulong? WorkerAddress { get; set; }

    public virtual bool IsVisible => true;
}
