using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Events;

public partial record EngineEvent
{
    public int DatabaseId { get; set; }

    [EventProperty("Sequence")]
    public int SequenceId { get; set; }

    [EventProperty("Timestamp")]
    public DateTime Timestamp { get; set; }

    public virtual string Name { get; set; } = string.Empty;

    [EventProperty("Time", Type = EventPropertyType.Microseconds)]
    public virtual long TimeUs { get; set; }

    [EventProperty("Duration", Type = EventPropertyType.Microseconds)]
    public virtual long DurationUs { get; set; }

    public AllocationUnit? AllocationUnit { get; set; }

    public virtual int ObjectId => AllocationUnit?.ObjectId ?? 0;

    [EventProperty("Object")]
    public virtual string ObjectName => AllocationUnit?.DisplayName ?? string.Empty;

    public virtual string SchemaName => AllocationUnit?.SchemaName ?? string.Empty;

    public virtual string TableName => AllocationUnit?.TableName ?? string.Empty;

    public virtual string IndexName => AllocationUnit?.IndexName ?? string.Empty;

    internal short PlanHandleId { get; set; }

    [EventProperty("Thread")]
    public int ThreadId { get; set; }

    public EventCategory? Category { get; set; }

    public virtual string Description => string.Empty;

    public virtual string Detail => Description;

    public PlanNodeIdentifier? PlanNodeIdentifier { get; set; }

    [EventProperty("Call Stack")]
    public CallStackNode? CallStack { get; set; }

    public EngineEvent? FoldedFrom { get; set; }

    [EventProperty("Task Address", Type = EventPropertyType.Address)]
    public ulong? TaskAddress { get; set; }

    [EventProperty("Worker Address", Type = EventPropertyType.Address)]
    public ulong? WorkerAddress { get; set; }

    public virtual bool IsVisible => true;

    public IReadOnlyList<EventProperty> GetProperties()
    {
        var properties = new List<EventProperty>();

        CollectProperties(properties);

        return properties;
    }
}
