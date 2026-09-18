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

        foreach (var pool in events.OfType<ObjectPoolEvent>().OrderBy(e => e.SequenceId))
        {
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

        var iamPages = IamPages(events);

        foreach (var read in events.OfType<ReadEventGroup>())
        {
            read.PoolLookup = OwnerOf(read, lookupsByPage) ?? (IsAllocationRead(read, iamPages) ? NextMiss(read, missesByTask) : null);
        }

        foreach (var task in events.OfType<ReadEventGroup>().Where(r => r.ReadType != ReadType.Cached).GroupBy(TaskOf))
        {
            RelinkCollateralReads([.. task.OrderBy(r => r.SequenceId)], missesByTask, lookupsByPage);
        }
    }

    private static void RelinkCollateralReads(List<ReadEventGroup> reads,
                                              Dictionary<ulong, List<ObjectPoolEvent>> missesByTask,
                                              Dictionary<PageAddress, List<ObjectPoolEvent>> lookupsByPage)
    {
        for (var i = 0; i < reads.Count; i++)
        {
            var read = reads[i];

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

    private static bool IsAllocationRead(ReadEventGroup read, HashSet<PageAddress> iamPages)
    {
        foreach (var page in read.Pages)
        {
            if (iamPages.Contains(page) || PageNameHelper.TryGetPageName(page) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<PageAddress> IamPages(IReadOnlyList<EngineEvent> events)
    {
        var pages = new HashSet<PageAddress>();

        var units = new HashSet<long>();

        foreach (var engineEvent in events)
        {
            if (engineEvent.AllocationUnit is not { } unit || !units.Add(unit.AllocationUnitId))
            {
                continue;
            }

            foreach (var iam in unit.IamChain.Pages)
            {
                pages.Add(iam.PageAddress);
            }
        }

        return pages;
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
}
