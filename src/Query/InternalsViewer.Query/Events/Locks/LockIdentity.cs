namespace InternalsViewer.Query.Events.Locks;

/// <summary>
/// Unique identify for a lock
/// </summary>
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