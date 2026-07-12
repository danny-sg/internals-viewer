using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Plans;

namespace InternalsViewer.Query.Events.EventTypes;

public record EngineEvent
{
    public int DatabaseId { get; set; }

    public int SequenceId { get; set; }

    public DateTime Timestamp { get; set; }

    public string Name { get; set; } = string.Empty;

    public virtual long TimeUs { get; set; }

    public virtual long DurationUs { get; set; }

    public int ObjectId { get; set; }

    public string ObjectName { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string IndexName { get; set; } = string.Empty;

    internal short PlanHandleId { get; set; }

    public int ThreadId { get; set; }

    public EventCategory? Category { get; set; }

    public virtual string Description => string.Empty;

    public virtual string Detail => Description;

    public PlanNodeIdentifier? PlanNodeIdentifier { get; set; }

    /// <summary>
    /// The leaf node of this event's path in the shared call stack tree (null if no stack was captured)
    /// </summary>
    /// <remarks>
    /// The frames themselves live once in <see cref="Callstack.CallStackTree"/>; walk <see cref="CallStackNode.Parent"/>
    /// from here for this event's path.
    /// </remarks>
    public CallStackNode? CallStack { get; set; }

    public ulong? TaskAddress { get; set; }

    public ulong? WorkerAddress { get; set; }
}
