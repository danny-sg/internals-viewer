using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Consolidation step that gives an unpaired (never-released) lock acquire its held duration
/// </summary>
/// <remarks>
/// A lock whose release is not captured — the fine-grained key/page locks a statement takes, held past the traced
/// window — folds to a zero-duration open acquire in <see cref="IntervalCollapser"/>, so it renders as an invisible
/// point rather than a held bar. Its real hold ends at one of two things:
///
///   - ESCALATION: when the owning transaction takes a coarse object-level lock in a superseding (non-intent) mode, it
///     drops the finer key/page locks it replaces — so a finer lock acquired before that point is held only until it.
///   - COMMIT (outside the window): everything else (the object lock itself, or a transaction that never escalated) is
///     held to the end of the captured statement.
///
/// Works off the owning transaction + resource type + mode alone (no allocation-unit resolution), so it runs on the
/// collapsed stream before <see cref="ReadGrouping"/>/<see cref="LockGrouping"/> and the group span then reflects the
/// closed holds. Paired locks (a real release was captured) are left untouched.
/// </remarks>
public static class HeldLockCloser
{
    public static void Close(IReadOnlyList<EngineEvent> events)
    {
        var statementEnd = 0L;

        foreach (var e in events)
        {
            if (e.TimeUs > statementEnd)
            {
                statementEnd = e.TimeUs;
            }
        }

        var byTransaction = events.OfType<LockEvent>()
                                  .Where(l => l.LockOwnerContext?.TransactionId is > 0)
                                  .GroupBy(l => l.LockOwnerContext!.TransactionId!.Value);

        foreach (var transaction in byTransaction)
        {
            // The escalation point: the earliest object-level lock the transaction took in a mode that supersedes the
            // finer locks (a full X/U/S table lock — intent locks coexist with the fine locks, so they don't count).
            var escalation = transaction
                .Where(l => GranularityLevel(l.Resource.ResourceType) == ObjectLevel
                            && LockModeClassifier.IsSuperseding(l.LockMode))
                .Select(l => (long?)l.TimeUs)
                .Min();

            foreach (var lockEvent in transaction)
            {
                if (lockEvent.Name != "lock_acquired" || lockEvent.DurationUs > 0
                    || !IsDataLock(lockEvent.Resource.ResourceType))
                {
                    continue;
                }

                var end = GranularityLevel(lockEvent.Resource.ResourceType) < ObjectLevel
                          && escalation is { } escalationTime && escalationTime > lockEvent.TimeUs
                    ? escalationTime
                    : statementEnd;

                lockEvent.DurationUs = Math.Max(0, end - lockEvent.TimeUs);

                // Fold to a resolved hold like a paired lock — it is no longer an open acquire.
                lockEvent.Name = "Lock";
            }
        }
    }

    private const int ObjectLevel = 2;

    // Row -> 0, page -> 1, object and coarser -> 2 (mirrors the timeline's escalation lanes).
    private static int GranularityLevel(LockResourceType type) => type switch
    {
        LockResourceType.Key or LockResourceType.Rid => 0,
        LockResourceType.Page or LockResourceType.Extent => 1,
        _ => ObjectLevel,
    };

    // Transaction data locks (held to escalation / commit); database / metadata / application locks are transient and
    // excluded so a missing release there is not painted as a full-statement hold.
    private static bool IsDataLock(LockResourceType type) => type is
        LockResourceType.Key or LockResourceType.Rid or LockResourceType.Page or LockResourceType.Extent
        or LockResourceType.Object or LockResourceType.Hobt or LockResourceType.AllocationUnit or LockResourceType.Rowgroup;

}
