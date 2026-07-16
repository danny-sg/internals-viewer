using InternalsViewer.Internals.Engine.Address;
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
    /// Set by <see cref="Consolidation.LockPartitionCollapser"/>: an object lock in a non-intent mode is acquired on every partition, and
    /// those are collapsed into this one logical lock.
    /// </remarks>
    public int PartitionCount { get; set; } = 1;

    public LockIdentity Identity => new(Resource.Key,
                                        LockMode,
                                        LockOwnerContext?.OwnerType ?? LockOwnerType.Unknown,
                                        LockOwnerContext?.WorkspaceId ?? 0,
                                        LockOwnerContext?.SubId ?? 0,
                                        LockOwnerContext?.NestId ?? 0,
                                        LockOwnerContext?.TransactionId);

    public override int ObjectId => Resource.ObjectId;

    public override PageAddress? PageAddress => Resource.PageAddress ?? Resource.RowIdentifier?.PageAddress;

    public override string ObjectName =>
        AllocationUnit?.DisplayName ?? (Resource.ObjectId > 0 ? $"(Object Id {Resource.ObjectId})" : base.ObjectName);

    public override string Description
    {
        get
        {
            var description = $"Lock: {Resource.ResourceType} {LockMode} ({EventItemName.Get(LockMode)}) - {GetResourceDescription()}";

            if (PartitionCount > 1)
            {
                return $"{description} across {PartitionCount} lock partitions";
            }

            return description;
        }
    }

    private string? GetResourceDescription()
    {
        switch (Resource.ResourceType)
        {
            case LockResourceType.Page:
                return PageAddress?.ToString();
            case LockResourceType.Rid:
                return Resource.RowIdentifier?.ToString();
            case LockResourceType.Extent:
            case LockResourceType.Hobt:
                return $"{AllocationUnit?.PartitionId} - {AllocationUnit?.DisplayName}";
            case LockResourceType.Key:
                return $"{Resource.Key}";
            default:
                return AllocationUnit?.DisplayName;
        }
    }
}