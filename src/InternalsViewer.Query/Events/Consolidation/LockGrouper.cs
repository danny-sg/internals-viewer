using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Consolidation step that binds a transaction's locks on one object into a single <see cref="LockGroup"/>
/// </summary>
/// <remarks>
/// Runs after <see cref="IntervalCollapser"/> (acquire/release already folded) and the AU enrichment (so each lock
/// knows its object). Escalation moves an owner's locks UP the granularity on the SAME object (rid → page → object),
/// so the object — the resolved <c>AllocationUnit.ObjectId</c> — plus the owning transaction is the group key. Schema
/// locks (SCH_S/SCH_M) protect the object's shape rather than its rows, so they group SEPARATELY from the data-lock
/// escalation chain (one "Object Schema Locks" group, one "Object Locks" group). Locks with no resolved object
/// (metadata/database) or no transaction are left as individual events, as are lone locks.
/// </remarks>
public static class LockGrouper
{
    public static List<EngineEvent> Group(IReadOnlyList<EngineEvent> events)
    {
        var byKey = new Dictionary<(int ObjectId, long TransactionId, bool IsSchema), List<LockEvent>>();

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

        foreach (var (key, locks) in byKey)
        {
            if (locks.Count < 2)
            {
                continue;
            }

            var ordered = locks.OrderBy(l => l.SequenceId).ToList();

            groupByAnchor[ordered[0]] = BuildGroup(ordered, key.IsSchema);

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

    // The object (via the resolved allocation unit), owning transaction, and whether it is a schema lock — schema locks
    // group apart from the data-lock chain. Null when the lock has no resolved object or no transaction.
    private static (int ObjectId, long TransactionId, bool IsSchema)? KeyOf(LockEvent lockEvent)
    {
        if (lockEvent.AllocationUnit is not { ObjectId: > 0 } allocationUnit)
        {
            return null;
        }

        return lockEvent.LockOwnerContext?.TransactionId is { } transactionId and > 0
            ? (allocationUnit.ObjectId, transactionId, IsSchemaLock(lockEvent))
            : null;
    }

    // Schema-stability/modification locks guard the object's shape (DDL vs the running query), not its rows, so they are
    // a distinct concern from the data-lock escalation chain.
    private static bool IsSchemaLock(LockEvent lockEvent) =>
        lockEvent.LockMode is LockMode.SCH_S or LockMode.SCH_M;

    private static LockGroup BuildGroup(List<LockEvent> locks, bool isSchema)
    {
        var start = locks.Min(l => l.TimeUs);
        var end = locks.Max(l => l.TimeUs + l.DurationUs);

        var representative = locks[0];

        return new LockGroup
        {
            Name = isSchema ? "Object Schema Locks" : "Object Locks",
            Events = locks,
            SequenceId = representative.SequenceId,
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

    private static List<LockEvent> Bucket(Dictionary<(int, long, bool), List<LockEvent>> map, (int, long, bool) key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];

            map[key] = list;
        }

        return list;
    }
}
