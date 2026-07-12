using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Events.Locks;

public sealed record LockEvent : PageEngineEvent
{
    public LockMode LockMode { get; init; }

    public LockResourceType ResourceType { get; init; }

    public RowIdentifier? RowIdentifier { get; set; }

    public string? KeyHash { get; set; }

    // The lock's raw object id (from the event) — a lock is on an object, not an allocation unit, and it drives both
    // the enrichment lookup and the key-hash grouping, so it is kept even when no allocation unit resolves.
    public int LockObjectId { get; init; }

    public override int ObjectId => LockObjectId;

    public override string ObjectName =>
        AllocationUnit?.DisplayName ?? (LockObjectId > 0 ? $"(Object Id {LockObjectId})" : base.ObjectName);

    public override string Description => $"Lock: {LockMode}/{ResourceType}";
}