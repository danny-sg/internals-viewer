using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events.Locks;

/// <summary>
/// Defines the resource that a lock is being held on
/// </summary>
public sealed record LockResource
{
    public LockResourceType ResourceType { get; init; }

    /// <summary>
    /// Internally generated key/hash to uniquely identify the lock
    /// </summary>
    public ulong Key { get; init; }

    public PageAddress? PageAddress { get; init; }

    public int ObjectId { get; init; }

    /// <summary>
    /// HoBT Id reference associated with KEY and HOBT resource types
    /// </summary>
    /// <remarks>
    /// HoBT Id is partition_id in allocation units
    /// </remarks>
    public long? HobtId { get; init; }

    /// <summary>
    /// Row Identifier (RID) for RID locks
    /// </summary>
    public RowIdentifier? RowIdentifier { get; set; }

    /// <summary>
    /// Key Hash referenced in KEY locks
    /// </summary>
    /// <remarks>
    /// The Key Hash is calculated internally in SQL Server but can be referenced using the <c>%%lockres%%</c> virtual column in a query
    /// </remarks>
    public string? KeyHash { get; init; }

    /// <summary>
    /// Lock partition the lock was taken on
    /// </summary>
    /// <remarks>
    /// SQL Server partitions OBJECT locks across schedulers on a machine with 16+ CPUs, one partition per CPU. Intent modes take a single
    /// partition (the current scheduler's) - <see cref="Consolidation.LockPartitionCollapser"/>.
    /// </remarks>
    public int? LockPartition { get; init; }
}