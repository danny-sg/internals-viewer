using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Transactions;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Consolidation step to give an unpaired (never-released) lock acquire a held duration
/// </summary>
/// <remarks>
/// A lock whose release is not captured — the fine-grained key/page locks a statement takes, held past the traced window — folds to a
/// zero-duration open acquire in <see cref="IntervalCollapser"/>, so it renders as an invisible point rather than a held bar.
///
/// Its real hold ends at one of two things:
///
///   - Escalation: when the owning transaction takes a coarse object-level lock in a superseding (non-intent) mode, it
///     drops the finer key/page locks it replaces — so a finer lock acquired before that point is held only until it.
/// 
///   - Transaction commit (outside the window): everything else (the object lock itself, or a transaction that never escalated) is held to
///     the end of the captured statement.
///
/// Works off the owning transaction + resource type + mode alone (no allocation-unit resolution), so it runs on the collapsed stream
/// before <see cref="ReaderGrouper"/>/<see cref="LockGrouper"/> and the group span then reflects the closed holds. Paired locks (a real
/// release was captured) are left untouched.
/// </remarks>
public static class HeldLockCloser
{
    public static void Close(IReadOnlyList<EngineEvent> events)
    {
        var statementEnd = 0L;

        // The statement ends when the last interval ends — the collapsed stream carries durations, so the tail event (a long read or a
        // folded hold) reaches past its own start, and a lock acquired at that start is held across it.
        foreach (var e in events)
        {
            var end = e.TimeUs + e.DurationUs;

            if (end > statementEnd)
            {
                statementEnd = end;
            }
        }

        // Measured signals, keyed by transaction: when escalation actually fired, and when the transaction actually ended. Both beat the
        // inferences below, which only exist for when the events were not captured.
        var escalations = events.OfType<LockEscalationEvent>()
                                .Where(e => e.TransactionId is > 0)
                                .GroupBy(e => e.TransactionId!.Value)
                                .ToDictionary(g => g.Key, g => g.Min(e => e.TimeUs));

        var transactionEnds = events.OfType<TransactionEvent>()
                                    .Where(t => t is { IsEnd: true, TransactionId: > 0 })
                                    .GroupBy(t => t.TransactionId)
                                    .ToDictionary(g => g.Key, g => g.Max(t => t.TimeUs));

        var byTransaction = events.OfType<LockEvent>()
                                  .Where(l => l.LockOwnerContext?.TransactionId is > 0)
                                  .GroupBy(l => l.LockOwnerContext!.TransactionId!.Value);

        foreach (var transaction in byTransaction)
        {
            // Prefer the measured escalation moment; otherwise infer it as the earliest object-level lock the transaction took in a mode
            // that supersedes the finer locks (a full X/U/S table lock — intent locks coexist with the fine locks, so they don't count).
            var escalation = escalations.TryGetValue(transaction.Key, out var escalated)
                ? escalated
                : transaction.Where(l => GranularityLevel(l.Resource.ResourceType) == ObjectLevel
                                         && LockModeClassifier.IsSuperseding(l.LockMode))
                             .Select(l => (long?)l.TimeUs)
                             .Min();

            // Prefer the transaction's own commit/rollback; otherwise the end of the captured statement
            var transactionEnd = transactionEnds.TryGetValue(transaction.Key, out var committed)
                ? (long?)committed
                : null;

            foreach (var lockEvent in transaction)
            {
                if (lockEvent.Name != "lock_acquired" || lockEvent.DurationUs > 0
                    || !IsDataLock(lockEvent.Resource.ResourceType))
                {
                    continue;
                }

                // At-or-after, not strictly after: escalation drops every finer lock the transaction already holds, including any taken in
                // the same instant. Timestamps resolve to the millisecond, so the last lock before an escalation routinely share its
                // bucket — strictly-after would send exactly those to the statement end and leave them held for the whole trace.
                var end = GranularityLevel(lockEvent.Resource.ResourceType) < ObjectLevel
                          && escalation is { } escalationTime && escalationTime >= lockEvent.TimeUs
                          ? escalationTime
                          : CommitOrStatementEnd(lockEvent, transactionEnd, statementEnd);

                lockEvent.DurationUs = Math.Max(0, end - lockEvent.TimeUs);

                // Fold to a resolved hold like a paired lock — it is no longer an open acquire
                lockEvent.Name = "Lock";
            }
        }
    }

    /// <summary>
    /// Gets the end time for an event based on commit time/statement end time
    /// </summary>
    /// <remarks>
    /// The measured commit/rollback is only trusted when it actually falls after the lock — that guards against a mis-mapped
    /// transaction_state (it degrades to the statement end rather than producing a nonsense duration).
    /// </remarks>
    private static long CommitOrStatementEnd(LockEvent lockEvent, long? transactionEnd, long statementEnd) =>
        transactionEnd is { } end && end > lockEvent.TimeUs ? end : statementEnd;

    private const int ObjectLevel = 2;

    // Row -> 0, page -> 1, object and coarser -> 2 (mirrors the timeline's escalation lanes).
    private static int GranularityLevel(LockResourceType type) => type switch
    {
        LockResourceType.Key or LockResourceType.Rid => 0,
        LockResourceType.Page or LockResourceType.Extent => 1,
        _ => ObjectLevel,
    };

    /// <summary>
    /// If a lock resource type is a transaction data lock
    /// </summary>
    /// <remarks>
    /// Identifies data locks vs database\metadata\application locks which are transient. For non-data locks a missing lock release is not
    /// considered held for the full statement.
    /// </remarks>
    private static bool IsDataLock(LockResourceType type) => type is
        LockResourceType.Key or LockResourceType.Rid or LockResourceType.Page or LockResourceType.Extent
        or LockResourceType.Object or LockResourceType.Hobt or LockResourceType.AllocationUnit or LockResourceType.Rowgroup;

}
