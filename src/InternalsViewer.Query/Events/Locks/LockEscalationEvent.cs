using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Events.Locks;

/// <summary>
/// Event to mark when a transaction's fine-grained locks on an object were escalated to a single coarse lock
/// </summary>
/// <remarks>
/// Escalation replaces the key/page locks it covers — they are bulk-dropped with no <c>lock_released</c> so this event is the measured
/// point at which those holds end (see <see cref="Consolidation.HeldLockCloser"/>)
/// </remarks>
public sealed record LockEscalationEvent : EngineEvent
{
    public LockMode LockMode { get; init; }

    public LockResourceType ResourceType { get; init; }

    public int EscalatedObjectId { get; init; }

    public long? TransactionId { get; init; }

    /// <summary>
    /// Number of finer locks the escalation dropped
    /// </summary>
    public long EscalatedLockCount { get; init; }

    /// <summary>
    /// Locks held on the HoBT (heap or b-tree) at the point of escalation
    /// </summary>
    public long HobtLockCount { get; init; }

    public override int ObjectId => EscalatedObjectId;

    public override string ObjectName =>
        AllocationUnit?.DisplayName ?? (EscalatedObjectId > 0 ? $"(Object Id {EscalatedObjectId})" : base.ObjectName);

    public override string Description =>
        $"Lock escalation: {LockMode} ({EventItemName.Get(LockMode)}) on {ResourceType}"
        + (EscalatedLockCount > 0 ? $", replacing {EscalatedLockCount} lock(s)" : string.Empty);
}
