using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.Events.BatchMode;

/// <summary>
/// Links each read to the object pool lookup it fed, so the read belongs to the pool rather than to the operator
/// </summary>
/// <remarks>
/// A read of a page a lookup owns is that object being built. Where the object was looked up more than once the read
/// belongs to the next lookup after it, since the reads come before the miss that reports them, and failing that to the
/// last one before it.
///
/// An allocation page is owned by no object. Opening a LOB checks the page's allocation status, which reads the PFS and
/// IAM pages, so a read that owns nothing and touches an allocation page belongs to the next miss on the same task.
/// </remarks>
public static class ObjectPoolReadLinker
{
    public static void Link(IReadOnlyList<EngineEvent> events)
    {
        var lookupsByPage = new Dictionary<PageAddress, List<ObjectPoolEvent>>();

        var lookupsByTask = new Dictionary<ulong, List<ObjectPoolEvent>>();

        foreach (var pool in events.OfType<ObjectPoolEvent>().OrderBy(e => e.SequenceId))
        {
            GetOrAdd(lookupsByTask, pool.TaskKey()).Add(pool);

            foreach (var page in pool.Pages)
            {
                GetOrAdd(lookupsByPage, page).Add(pool);
            }
        }

        List<ReadEventGroup> reads = [.. events.OfType<ReadEventGroup>().OrderBy(r => r.SequenceId)];

        foreach (var read in reads)
        {
            read.PoolLookup = read.IsAllocationPage ? null : OwnerOf(read, lookupsByPage);
        }

        if (lookupsByTask.Count == 0)
        {
            return;
        }

        foreach (var task in reads.GroupBy(r => r.TaskKey()))
        {
            List<ReadEventGroup> taskReads = [.. task];

            RelinkCollateralReads([.. taskReads.Where(r => r.ReadType != ReadType.Cached)], lookupsByTask, lookupsByPage);

            MarkReadAhead(taskReads, lookupsByTask.GetValueOrDefault(task.Key) ?? [], lookupsByPage);
        }
    }

    private static void RelinkCollateralReads(List<ReadEventGroup> reads,
                                              Dictionary<ulong, List<ObjectPoolEvent>> lookupsByTask,
                                              Dictionary<PageAddress, List<ObjectPoolEvent>> lookupsByPage)
    {
        for (var i = 0; i < reads.Count; i++)
        {
            var read = reads[i];

            if (read.IsReadAhead)
            {
                continue;
            }

            var building = NextMiss(read, lookupsByTask);

            if (building is null || ReferenceEquals(read.PoolLookup, building))
            {
                continue;
            }

            var previous = i > 0 ? reads[i - 1] : null;

            var next = i < reads.Count - 1 ? reads[i + 1] : null;

            if (IsSameRead(read, previous, building, lookupsByPage) || IsSameRead(read, next, building, lookupsByPage))
            {
                read.PoolLookup = building;
            }
        }
    }

    private static bool IsSameRead(ReadEventGroup read,
                                   ReadEventGroup? neighbour,
                                   ObjectPoolEvent building,
                                   Dictionary<PageAddress, List<ObjectPoolEvent>> lookupsByPage)
    {
        if (neighbour is null || neighbour.IsReadAhead || !ReferenceEquals(neighbour.PoolLookup, building) || read.Pages.Count == 0)
        {
            return false;
        }

        if (!neighbour.Pages.Any(p => lookupsByPage.TryGetValue(p, out var lookups) && lookups.Contains(building)))
        {
            return false;
        }

        if (read.Pages[0].FileId != neighbour.Pages[0].FileId)
        {
            return false;
        }

        var first = read.Pages.Min(p => p.PageId);

        var last = read.Pages.Max(p => p.PageId);

        return neighbour.Pages.Min(p => p.PageId) == last + 1 || neighbour.Pages.Max(p => p.PageId) == first - 1;
    }

    private static ObjectPoolEvent? OwnerOf(ReadEventGroup read, Dictionary<PageAddress, List<ObjectPoolEvent>> lookupsByPage)
    {
        ObjectPoolEvent? next = null;

        ObjectPoolEvent? previous = null;

        foreach (var page in read.Pages)
        {
            if (!lookupsByPage.TryGetValue(page, out var lookups))
            {
                continue;
            }

            foreach (var lookup in lookups)
            {
                if (lookup.SequenceId > read.SequenceId)
                {
                    if (next is null || lookup.SequenceId < next.SequenceId)
                    {
                        next = lookup;
                    }
                }
                else if (previous is null || lookup.SequenceId > previous.SequenceId)
                {
                    previous = lookup;
                }
            }
        }

        return next ?? previous;
    }

    private static void MarkReadAhead(List<ReadEventGroup> reads,
                                      List<ObjectPoolEvent> lookups,
                                      Dictionary<PageAddress, List<ObjectPoolEvent>> lookupsByPage)
    {
        List<EngineEvent> sequence = [.. reads.Concat<EngineEvent>(lookups).OrderBy(e => e.SequenceId)];

        var ownerSequences = new int[sequence.Count];

        for (var i = 0; i < sequence.Count; i++)
        {
            ownerSequences[i] = sequence[i] is ReadEventGroup { PoolLookup: { } owner } read && OwnsAnyPage(owner, read, lookupsByPage)
                ? owner.SequenceId
                : -1;
        }

        for (var i = 0; i < sequence.Count; i++)
        {
            if (sequence[i] is not ReadEventGroup { PoolLookup: { } owner, IsReadAhead: false } read || HasCallStack(read))
            {
                continue;
            }

            for (var j = i + 1; j < sequence.Count && sequence[j].SequenceId < owner.SequenceId; j++)
            {
                if (sequence[j] is ObjectPoolEvent || ownerSequences[j] > owner.SequenceId)
                {
                    read.IsReadAhead = true;

                    break;
                }
            }
        }
    }

    private static bool HasCallStack(ReadEventGroup read) => read.CallStack is not null || read.Events.Any(e => e.CallStack is not null);

    private static bool OwnsAnyPage(ObjectPoolEvent owner, ReadEventGroup read, Dictionary<PageAddress, List<ObjectPoolEvent>> lookupsByPage)
        => read.Pages.Any(p => lookupsByPage.TryGetValue(p, out var lookups) && lookups.Contains(owner));

    private static ObjectPoolEvent? NextMiss(ReadEventGroup read, Dictionary<ulong, List<ObjectPoolEvent>> lookupsByTask)
    {
        if (!lookupsByTask.TryGetValue(read.TaskKey(), out var lookups))
        {
            return null;
        }

        foreach (var lookup in lookups)
        {
            if (!lookup.IsHit
                && lookup.SequenceId > read.SequenceId
                && (lookup.PlanNodeIdentifier is null
                    || read.PlanNodeIdentifier is null
                    || lookup.PlanNodeIdentifier == read.PlanNodeIdentifier))
            {
                return lookup;
            }
        }

        return null;
    }

    private static List<ObjectPoolEvent> GetOrAdd<TKey>(Dictionary<TKey, List<ObjectPoolEvent>> dictionary, TKey key) where TKey : notnull
    {
        if (!dictionary.TryGetValue(key, out var list))
        {
            list = [];

            dictionary[key] = list;
        }

        return list;
    }
}
