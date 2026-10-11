using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Consolidation step that folds an object lock's per-partition sweep into the single logical lock it represents
/// </summary>
/// <remarks>
/// SQL Server partitions OBJECT locks across schedulers on a machine with 16 or more CPUs, one partition per CPU. Intent
/// modes (IS/IU/IX) are taken on a single partition — the current scheduler's — but every other mode must be acquired on
/// EVERY partition, so a TABLOCKX surfaces as one X lock per CPU instead of the one table lock that was asked for. Only
/// those sweeping modes are folded; a lone intent lock is already the whole story.
///
/// Runs after <see cref="IntervalCollapser"/> and <see cref="HeldLockCloser"/>, so every partition's acquire/release is
/// already paired and timed — the partition stays part of the lock's resource key precisely so that pairing is
/// per-partition and never crossed. The fold takes the sweep's ENVELOPE (earliest acquire to latest release) rather than
/// partition 0's own times: partition 0 is only guaranteed to exist for a sweep, and the envelope degenerates correctly
/// when a partition's event is missing. It is microseconds wider than the "held on every partition" window, which is
/// what the timeline and allocation map want.
/// </remarks>
public static class LockPartitionCollapser
{
    public static List<EngineEvent> Collapse(IReadOnlyList<EngineEvent> events)
    {
        var byKey = new Dictionary<(int ObjectId, LockMode Mode, long TransactionId), List<LockEvent>>();

        foreach (var e in events)
        {
            if (e is LockEvent lockEvent && KeyOf(lockEvent) is { } key)
            {
                Bucket(byKey, key).Add(lockEvent);
            }
        }

        var consumed = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        foreach (var (_, locks) in byKey)
        {
            // A single partition is not a sweep — the object was locked once, leave it alone.
            if (locks.Count < 2)
            {
                continue;
            }

            var ordered = locks.OrderBy(l => l.TimeUs).ToList();

            // The earliest acquire keeps its place and takes the sweep's span; the rest are consumed into it.
            var anchor = ordered[0];

            var end = ordered.Max(l => l.TimeUs + l.DurationUs);

            anchor.DurationUs = Math.Max(0, end - anchor.TimeUs);
            anchor.PartitionCount = ordered.Count;

            for (var i = 1; i < ordered.Count; i++)
            {
                consumed.Add(ordered[i]);
            }
        }

        var result = new List<EngineEvent>(events.Count);

        foreach (var e in events)
        {
            if (!consumed.Contains(e))
            {
                result.Add(e);
            }
        }

        return result;
    }

    // A partitioned object lock, in a mode that sweeps every partition, owned by a transaction. Null for anything else.
    private static (int ObjectId, LockMode Mode, long TransactionId)? KeyOf(LockEvent lockEvent)
    {
        if (lockEvent.Resource is not { ResourceType: LockResourceType.Object, LockPartition: not null } resource
            || !SweepsAllPartitions(lockEvent.LockMode))
        {
            return null;
        }

        return lockEvent.LockOwnerContext?.TransactionId is { } transactionId and > 0
            ? (resource.ObjectId, lockEvent.LockMode, transactionId)
            : null;
    }

    // Intent modes take one partition (the current scheduler's); every other mode is acquired on all of them.
    private static bool SweepsAllPartitions(LockMode mode) =>
        mode is not (LockMode.IS or LockMode.IU or LockMode.IX);

    private static List<LockEvent> Bucket(Dictionary<(int, LockMode, long), List<LockEvent>> map,
                                          (int, LockMode, long) key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];

            map[key] = list;
        }

        return list;
    }
}
