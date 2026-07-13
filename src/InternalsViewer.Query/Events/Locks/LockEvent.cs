using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Events.Locks;

public sealed record LockEvent : PageEngineEvent
{
    public LockMode LockMode { get; init; }

    public required LockResource Resource { get; init; }

    public LockOwnerContext? LockOwnerContext { get; set; }

    public LockIdentity Identity => new(ResourceKey: Resource.Key,
                                        LockMode: LockMode,
                                        OwnerType: LockOwnerContext?.OwnerType ?? LockOwnerType.Unknown,
                                        WorkspaceId: LockOwnerContext?.WorkspaceId ?? 0,
                                        SubId: LockOwnerContext?.SubId ?? 0,
                                        NestId: LockOwnerContext?.NestId ?? 0,
                                        TransactionId: LockOwnerContext?.TransactionId
    );

    public override int ObjectId => Resource.ObjectId;

    public override string ObjectName =>
        AllocationUnit?.DisplayName ?? (Resource.ObjectId > 0 ? $"(Object Id {Resource.ObjectId})" : base.ObjectName);

    public override string Description => $"Lock: {LockMode}/{Resource.ResourceType}";
}

public readonly record struct LockIdentity
(
    ulong ResourceKey,
    LockMode LockMode,

    LockOwnerType OwnerType,
    ulong WorkspaceId,
    uint SubId,
    uint NestId,

    long? TransactionId
);