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

        var missesByTask = new Dictionary<ulong, List<ObjectPoolEvent>>();

        var lookupsByTask = new Dictionary<ulong, List<ObjectPoolEvent>>();

        foreach (var pool in events.OfType<ObjectPoolEvent>().OrderBy(e => e.SequenceId))
        {
            if (!lookupsByTask.TryGetValue(TaskOf(pool), out var taskLookups))
            {
                taskLookups = [];

                lookupsByTask[TaskOf(pool)] = taskLookups;
            }

            taskLookups.Add(pool);

            foreach (var page in pool.Pages)
            {
                if (!lookupsByPage.TryGetValue(page, out var lookups))
                {
                    lookups = [];

                    lookupsByPage[page] = lookups;
                }

                lookups.Add(pool);
            }

            if (pool.IsHit)
            {
                continue;
            }

            if (!missesByTask.TryGetValue(TaskOf(pool), out var misses))
            {
                misses = [];

                missesByTask[TaskOf(pool)] = misses;
            }

            misses.Add(pool);
        }

        if (missesByTask.Count == 0 && lookupsByPage.Count == 0)
        {
            return;
        }

        foreach (var read in events.OfType<ReadEventGroup>())
        {
            read.PoolLookup = null;

            read.ReadAheadFor = null;

            if (read.IsAllocationPage)
            {
                continue;
            }

            if (read.IsReadAhead)
            {
                read.ReadAheadFor = OwnerOf(read, lookupsByPage);

                continue;
            }

            read.PoolLookup = OwnerOf(read, lookupsByPage);
        }

        foreach (var task in events.OfType<ReadEventGroup>().Where(r => r.ReadType != ReadType.Cached).GroupBy(TaskOf))
        {
            RelinkCollateralReads([.. task.OrderBy(r => r.SequenceId)], missesByTask, lookupsByPage);
        }

        foreach (var task in events.OfType<ReadEventGroup>().GroupBy(TaskOf))
        {
            MarkOvertakenReads([.. task.OrderBy(r => r.SequenceId)], lookupsByPage);
        }

        foreach (var read in events.OfType<ReadEventGroup>())
        {
            if (read.PoolLookup is { } owner
                && OwnsAnyPage(owner, read, lookupsByPage)
                && IsFetchedAhead(read, owner, lookupsByTask))
            {
                read.IsReadAhead = true;

                read.ReadAheadFor = owner;

                read.PoolLookup = null;
            }
        }
    }

    private static void RelinkCollateralReads(List<ReadEventGroup> reads,
                                              Dictionary<ulong, List<ObjectPoolEvent>> missesByTask,
                                              Dictionary<PageAddress, List<ObjectPoolEvent>> lookupsByPage)
    {
        for (var i = 0; i < reads.Count; i++)
        {
            var read = reads[i];

            if (read.IsReadAhead)
            {
                continue;
            }

            var building = NextMiss(read, missesByTask);

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
        if (neighbour is null || !ReferenceEquals(neighbour.PoolLookup, building) || read.Pages.Count == 0)
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

    private static void MarkOvertakenReads(List<ReadEventGroup> reads, Dictionary<PageAddress, List<ObjectPoolEvent>> lookupsByPage)
    {
        var owners = new ObjectPoolEvent?[reads.Count];

        var ownerSequences = new int[reads.Count];

        for (var i = 0; i < reads.Count; i++)
        {
            owners[i] = reads[i].PoolLookup;

            ownerSequences[i] = owners[i] is { } owner && OwnsAnyPage(owner, reads[i], lookupsByPage) ? owner.SequenceId : -1;
        }

        var maximums = new RangeMaximum(ownerSequences);

        var overtaken = new List<int>();

        for (var i = 0; i < reads.Count; i++)
        {
            if (owners[i] is not { } owner || owner.SequenceId <= reads[i].SequenceId)
            {
                continue;
            }

            var end = FirstAtOrAfter(reads, owner.SequenceId);

            if (end > i + 1 && maximums.Query(i + 1, end - 1) > owner.SequenceId)
            {
                overtaken.Add(i);
            }
        }

        foreach (var i in overtaken)
        {
            reads[i].IsReadAhead = true;

            reads[i].ReadAheadFor = owners[i];

            reads[i].PoolLookup = null;
        }
    }

    private static bool OwnsAnyPage(ObjectPoolEvent owner, ReadEventGroup read, Dictionary<PageAddress, List<ObjectPoolEvent>> lookupsByPage)
        => read.Pages.Any(p => lookupsByPage.TryGetValue(p, out var lookups) && lookups.Contains(owner));

    private static int FirstAtOrAfter(List<ReadEventGroup> reads, int sequenceId)
    {
        var low = 0;

        var high = reads.Count;

        while (low < high)
        {
            var middle = (low + high) >> 1;

            if (reads[middle].SequenceId < sequenceId)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static bool IsFetchedAhead(ReadEventGroup read,
                                       ObjectPoolEvent owner,
                                       Dictionary<ulong, List<ObjectPoolEvent>> lookupsByTask)
    {
        if (owner.SequenceId < read.SequenceId || !lookupsByTask.TryGetValue(TaskOf(read), out var lookups))
        {
            return false;
        }

        foreach (var lookup in lookups)
        {
            if (lookup.SequenceId > read.SequenceId && lookup.SequenceId < owner.SequenceId)
            {
                return true;
            }
        }

        return false;
    }

    private static ObjectPoolEvent? NextMiss(ReadEventGroup read, Dictionary<ulong, List<ObjectPoolEvent>> missesByTask)
    {
        if (!missesByTask.TryGetValue(TaskOf(read), out var misses))
        {
            return null;
        }

        foreach (var miss in misses)
        {
            if (miss.SequenceId > read.SequenceId
                && (miss.PlanNodeIdentifier is null || read.PlanNodeIdentifier is null || miss.PlanNodeIdentifier == read.PlanNodeIdentifier))
            {
                return miss;
            }
        }

        return null;
    }

    private static ulong TaskOf(EngineEvent engineEvent)
        => engineEvent.TaskAddress ?? engineEvent.WorkerAddress ?? (ulong)engineEvent.ThreadId;

    private sealed class RangeMaximum
    {
        private int[][] Levels { get; }

        public RangeMaximum(int[] values)
        {
            var levels = new List<int[]> { values };

            for (var width = 1; width * 2 <= values.Length; width *= 2)
            {
                var previous = levels[^1];

                var next = new int[previous.Length - width];

                for (var i = 0; i < next.Length; i++)
                {
                    next[i] = Math.Max(previous[i], previous[i + width]);
                }

                levels.Add(next);
            }

            Levels = [.. levels];
        }

        public int Query(int from, int to)
        {
            var level = 31 - int.LeadingZeroCount(to - from + 1);

            return Math.Max(Levels[level][from], Levels[level][to - (1 << level) + 1]);
        }
    }
}
