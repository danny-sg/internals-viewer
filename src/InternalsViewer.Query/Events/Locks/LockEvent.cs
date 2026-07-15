using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Events.Locks;

public sealed record LockEvent : PageEngineEvent
{
    public LockMode LockMode { get; init; }

    public required LockResource Resource { get; init; }

    public LockOwnerContext? LockOwnerContext { get; set; }

    /// <summary>
    /// Number of lock partitions this lock represents — 1 unless a partition sweep was folded into it
    /// </summary>
    /// <remarks>
    /// Set by <see cref="Consolidation.LockPartitionCollapser"/>: an object lock in a non-intent mode is acquired on
    /// every partition, and those are collapsed into this one logical lock.
    /// </remarks>
    public int PartitionCount { get; set; } = 1;

    public LockIdentity Identity => new(ResourceKey: Resource.Key,
                                        LockMode: LockMode,
                                        OwnerType: LockOwnerContext?.OwnerType ?? LockOwnerType.Unknown,
                                        WorkspaceId: LockOwnerContext?.WorkspaceId ?? 0,
                                        SubId: LockOwnerContext?.SubId ?? 0,
                                        NestId: LockOwnerContext?.NestId ?? 0,
                                        TransactionId: LockOwnerContext?.TransactionId
    );

    public override int ObjectId => Resource.ObjectId;

    public override PageAddress? PageAddress => Resource.PageAddress ?? Resource.RowIdentifier?.PageAddress;

    public override string ObjectName =>
        AllocationUnit?.DisplayName ?? (Resource.ObjectId > 0 ? $"(Object Id {Resource.ObjectId})" : base.ObjectName);

    public override string Description
    {
        get
        {
            var description = $"Lock: {Resource.ResourceType} {LockMode} ({EventItemName.Get(LockMode)})";

            // A swept object lock stands for one lock per scheduler; say so rather than looking like a lone partition.
            return PartitionCount > 1 ? $"{description} across {PartitionCount} lock partitions" : description;
        }
    }
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