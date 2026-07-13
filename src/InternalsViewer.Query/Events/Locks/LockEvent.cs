using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Events.Locks;

public sealed record LockEvent : PageEngineEvent
{
    public LockMode LockMode { get; init; }

    public LockResourceType ResourceType { get; init; }

    public RowIdentifier? RowIdentifier { get; set; }

    public string? KeyHash { get; set; }

    public int LockObjectId { get; init; }

    public long? HobtId { get; set; }

    public LockOwnerContext? LockOwnerContext { get; set; }

    /// <summary>Identifies the locked resource (from resource_0/1/2 + type), so acquire/release pair in the IntervalCollapser</summary>
    public ulong Key { get; init; }

    public override int ObjectId => LockObjectId;

    public override string ObjectName =>
        AllocationUnit?.DisplayName ?? (LockObjectId > 0 ? $"(Object Id {LockObjectId})" : base.ObjectName);

    public override string Description => $"Lock: {LockMode}/{ResourceType}";
}

public sealed record LockResource
{
    public LockResourceType ResourceType { get; init; }

    public ulong ResourceKey { get; init; }

    public int ObjectId { get; init; }

    public long? HobtId { get; init; }

    public RowIdentifier? RowIdentifier { get; init; }

    public string? KeyHash { get; init; }
}

public sealed record LockOwnerContext
{
    public LockOwnerType OwnerType { get; set; }

    public long? TransactionId { get; init; }
    
    public int? SessionId { get; init; }

    public ulong WorkspaceId { get; set; }

    public uint SubId { get; set; }

    public uint NestId { get; set; }
}