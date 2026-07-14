using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events.Locks;

/// <summary>
/// Defines the resource that a lock is being held on
/// </summary>
public sealed record LockResource
{
    public LockResourceType ResourceType { get; init; }

    public ulong Key { get; init; }

    public PageAddress? PageAddress { get; init; }

    public int ObjectId { get; init; }

    public long? HobtId { get; init; }

    public RowIdentifier? RowIdentifier { get; set; }

    public string? KeyHash { get; init; }
}