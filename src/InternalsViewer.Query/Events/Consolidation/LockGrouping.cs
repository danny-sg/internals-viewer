using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Consolidation step that binds a transaction's locks on one object into a single <see cref="LockGroup"/>
/// </summary>
/// <remarks>
/// Runs after <see cref="IntervalCollapser"/> (acquire/release already folded) and the AU enrichment (so each lock
/// knows its object). Escalation moves an owner's locks UP the granularity on the SAME object (rid → page → object),
/// so the object — the resolved <c>AllocationUnit.ObjectId</c> — plus the owning transaction is the group key. Locks
/// with no resolved object (metadata/database) or no transaction are left as individual events, as are lone locks.
/// </remarks>
public static class LockGrouping
{
    public static List<EngineEvent> Group(IReadOnlyList<EngineEvent> events)
    {
        var byKey = new Dictionary<(int ObjectId, long TransactionId), List<LockEvent>>();

        foreach (var e in events)
        {
            if (e is LockEvent lockEvent && KeyOf(lockEvent) is { } key)
            {
                Bucket(byKey, key).Add(lockEvent);
            }
        }

        // The first lock (by capture order) of a multi-lock group is where the LockGroup takes its place; the rest are
        // consumed into it. A lone lock on an object is left as-is.
        var groupByAnchor = new Dictionary<EngineEvent, LockGroup>(ReferenceEqualityComparer.Instance);

        var consumed = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        foreach (var locks in byKey.Values)
        {
            if (locks.Count < 2)
            {
                continue;
            }

            var ordered = locks.OrderBy(l => l.SequenceId).ToList();

            groupByAnchor[ordered[0]] = BuildGroup(ordered);

            foreach (var lockEvent in ordered)
            {
                consumed.Add(lockEvent);
            }
        }

        var result = new List<EngineEvent>(events.Count);

        foreach (var e in events)
        {
            if (groupByAnchor.TryGetValue(e, out var group))
            {
                result.Add(group);
            }
            else if (!consumed.Contains(e))
            {
                result.Add(e);
            }
        }

        return result;
    }

    // The object (via the resolved allocation unit) and owning transaction a lock groups under, or null when it has no
    // resolved object or no transaction to attribute it to.
    private static (int ObjectId, long TransactionId)? KeyOf(LockEvent lockEvent)
    {
        if (lockEvent.AllocationUnit is not { ObjectId: > 0 } allocationUnit)
        {
            return null;
        }

        return lockEvent.LockOwnerContext?.TransactionId is { } transactionId and > 0
            ? (allocationUnit.ObjectId, transactionId)
            : null;
    }

    private static LockGroup BuildGroup(List<LockEvent> locks)
    {
        var start = locks.Min(l => l.TimeUs);
        var end = locks.Max(l => l.TimeUs + l.DurationUs);

        var representative = locks[0];

        return new LockGroup
        {
            Name = "Locks",
            Events = locks,
            TimeUs = start,
            DurationUs = Math.Max(0, end - start),
            Timestamp = representative.Timestamp,
            DatabaseId = representative.DatabaseId,
            AllocationUnit = representative.AllocationUnit,
            TaskAddress = representative.TaskAddress,
            WorkerAddress = representative.WorkerAddress,
            Category = representative.Category,
            PlanHandleId = representative.PlanHandleId,
        };
    }

    private static List<LockEvent> Bucket(Dictionary<(int, long), List<LockEvent>> map, (int, long) key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];

            map[key] = list;
        }

        return list;
    }
}
