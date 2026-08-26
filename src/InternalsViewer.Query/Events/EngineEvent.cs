using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Events;

public record EngineEvent
{
    public int DatabaseId { get; set; }

    public int SequenceId { get; set; }

    public DateTime Timestamp { get; set; }

    public virtual string Name { get; set; } = string.Empty;

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

    public EngineEvent? FoldedFrom { get; set; }

    public ulong? TaskAddress { get; set; }

    public ulong? WorkerAddress { get; set; }

    public virtual bool IsVisible => true;
}
