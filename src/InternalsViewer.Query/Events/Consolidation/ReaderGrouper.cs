using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Waits;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Read event consolidation
/// </summary>
/// <remarks>
/// Page reads are comprised of multiple events. This helps to group them together so the read can be used as a single unit, with the
/// component events embedded.
///
/// There are several types of reads depending on whether a page is cached in the buffer pool and what disk access method is used:
///
///     1. Buffer Pool Reads - Page is in the buffer pool (memory) and does not need to be read from disk
///     2. Contiguous Disk Reads - A single page is read at a time
///     3. Scatter/Gather Disk Reads - Multiple pages are read in one operation
///
/// This works by choosing a "spine" event - the event a consolidated event is going to be built around and factors in the timestamps
/// provided by the event knowing that they are only accurate to 1 millisecond, when the resolution needs to be in microseconds.
///
/// Latches are important for this process. The database engine will latch any pages it is going use so this is signal used. Latch events
/// have address which corresponds to the memory address of the page in the buffer pool. This will be consistent at minimum across the
/// latching for a single read (acquire/suspend/escalate/release etc.)
///
/// Note: This is based on observed behaviour of events and the call stack rather than anything documented, it could change from version to
/// version.
///
/// When a page is needed the BPool::Get method is called.
///
/// Buffer Pool Reads
/// -----------------
///
/// Buffer Pool reads are indicated by SH BUF latches on a page (shared latch on BUF structure).
///
/// There can also be EX BUF (exclusive) or KP BUF (keep) latches too, but this is less consistent.
///
/// Disk Reads
/// ----------
///
/// If a page is not in the buffer pool it will be loaded into it via a disk read.
///
/// A query could read a page more than once, so the first read could be from disk, and subsequent reads from the buffer pool.
///
/// There are two modes for disk reads:
///
/// - Contiguous     - Single page reads. Used when read-ahead is off (non-heap) or seeks
/// - Scatter/Gather - Multiple pages are read into the buffer pool. Used for scans where read-ahead is on or heap scans
///
/// Both modes give out a signal via a BUF SH latch suspend begin/end - this has duration that accurately corresponds to the read duration.
///
/// (Suspend is for an I/O latch and there will be an associated PAGEIOLATCH wait, but this isn't included in the grouping)
/// 
/// This suspend is when the page(s) are not in the buffer pool, and it switches to file based read - either contiguous or scatter/gather,
/// then the physical_page_read events should be at the end to show that the pages have been read from disk, and the read is complete.
/// </remarks>
public static class ReaderGrouper
{
    public static List<EngineEvent> Group(IReadOnlyList<EngineEvent> events)
    {
        var members = new Dictionary<EngineEvent, List<EngineEvent>>(ReferenceEqualityComparer.Instance);

        var consumed = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        CollectContiguousDiskReads(events, members, consumed);

        CollectScatterGatherReads(events, members, consumed);

        CollectBufferPoolReads(events, members, consumed);

        EstimateGatherDurations(members);

        var result = new List<EngineEvent>(events.Count);

        foreach (var e in events)
        {
            if (members.TryGetValue(e, out var readMembers))
            {
                result.Add(BuildGroup(e, readMembers));
            }
            else if (e is FileEvent { Size: 0 })
            {
                // An issued file read whose completed never folded in (Size stays 0) — the read was cancelled before
                // it finished (e.g. a TOP finished first), moved no pages, so it is dropped rather than shown bare.
                continue;
            }
            else if (!consumed.Contains(e))
            {
                result.Add(e);
            }
        }

        return result;
    }

    private static void CollectContiguousDiskReads(IReadOnlyList<EngineEvent> events,
                                                   Dictionary<EngineEvent, List<EngineEvent>> members,
                                                   HashSet<EngineEvent> consumed)
    {
        // Find spines for the grouping based on suspended latches
        var spines = events.OfType<LatchEvent>().Where(IsSuspend).ToList();

        if (spines.Count == 0)
        {
            return;
        }

        var byLatchAddress = spines.Where(s => s.LatchAddress is not null)
                                   .GroupBy(s => s.LatchAddress!.Value)
                                   .ToDictionary(g => g.Key, g => g.OrderBy(s => s.TimeUs)
                                   .ToList());

        var byPage = spines.Where(s => s.PageAddress is not null)
                           .GroupBy(s => s.PageAddress!.Value)
                           .ToDictionary(g => g.Key, g => g.OrderBy(s => s.TimeUs).ToList());

        foreach (var spine in spines)
        {
            members[spine] = [spine];
        }

        foreach (var e in events)
        {
            if (e is LatchEvent latch && IsSuspend(latch))
            {
                continue;
            }

            var spine = ResolveSuspend(e, byLatchAddress, byPage);

            if (spine is null)
            {
                continue;
            }

            members[spine].Add(e);

            consumed.Add(e);
        }
    }

    private static void CollectScatterGatherReads(IReadOnlyList<EngineEvent> events,
                                                  Dictionary<EngineEvent, List<EngineEvent>> members,
                                                  HashSet<EngineEvent> consumed)
    {
        // A read the contiguous pass already claimed still describes the page range it loaded, so it stays a spine here and
        // the group that took it is where its pages go. When the scan catches up with read-ahead it suspends on the read's
        // first page, and that suspend consumes the file read — without this the read keeps only the page that was waited
        // on and every other page it loaded is left to surface as an unrelated cached read.
        var owners = new Dictionary<FileEvent, EngineEvent>(ReferenceEqualityComparer.Instance);

        foreach (var (owner, group) in members)
        {
            foreach (var member in group.OfType<FileEvent>())
            {
                owners[member] = owner;
            }
        }

        // Find spines for the grouping based on file reads
        var spines = events.OfType<FileEvent>()
                           .Where(f => f is { Size: > 0, PageAddress: not null })
                           .ToList();

        if (spines.Count == 0)
        {
            return;
        }

        foreach (var spine in spines)
        {
            if (!owners.ContainsKey(spine))
            {
                members[spine] = [spine];
            }
        }

        foreach (var e in events)
        {
            if (consumed.Contains(e) || members.ContainsKey(e) || !IsGatherMember(e) || PageOf(e) is not { } page)
            {
                continue;
            }

            var spine = ContainingGather(spines, page, e.TimeUs);

            if (spine is null)
            {
                continue;
            }

            // An owned spine keeps its group's own spine: that is the suspend, which carries the SQL-measured read duration, so the pages
            // join it rather than splitting the one read across two groups.
            members[owners.GetValueOrDefault(spine, spine)].Add(e);

            consumed.Add(e);
        }
    }

    private static void CollectBufferPoolReads(IReadOnlyList<EngineEvent> events,
                                               Dictionary<EngineEvent, List<EngineEvent>> members,
                                               HashSet<EngineEvent> consumed)
    {
        var byAddress = new Dictionary<ulong, EngineEvent>();

        foreach (var (spine, group) in members)
        {
            if (spine is not FileEvent && !group.Any(IsFileReadMarker))
            {
                continue;
            }

            // Only a single-page non-cached read absorbs a trailing SH re-read (the load-then-immediately-reread of that one page).
            //
            // A multi-page read-ahead (gather) read must not swallow the scan's later SH reads of the pages it prefetched — those are the
            // scan iterator's own page reads and must surface as individual cached reads spread across the scan, not collapse into the
            // early prefetch. (For a big scan this is thousands of them.)
            if (DistinctPageCount(group) > 1)
            {
                continue;
            }

            foreach (var member in group)
            {
                if (member is LatchEvent { LatchAddress: { } address })
                {
                    byAddress[address] = spine;
                }
            }
        }

        foreach (var e in events)
        {
            if (e is not LatchEvent latch
                || consumed.Contains(latch)
                || members.ContainsKey(latch)
                || !IsCachedBufferAcquire(latch))
            {
                continue;
            }

            if (FoldTarget(latch, members, byAddress) is { } spine)
            {
                members[spine].Add(latch);

                consumed.Add(latch);
            }
            else
            {
                members[latch] = [latch];
            }
        }
    }

    // A page just read from disk stays in its buffer frame, so the scan's own SH read of it can land a scheduling
    // quantum after the load. This window bounds how far a bare SH acquire may trail a non-cached read and still count
    // as its tail rather than a genuinely separate cached re-read of the (still resident) page later in the query.
    private const long CachedFoldToleranceUs = 15_000;

    private static EngineEvent? FoldTarget(LatchEvent latch,
                                           Dictionary<EngineEvent, List<EngineEvent>> members,
                                           Dictionary<ulong, EngineEvent> nonCachedByAddress)
    {
        if (latch.LatchAddress is not { } address
            || latch.PageAddress is not { } page
            || !nonCachedByAddress.TryGetValue(address, out var spine))
        {
            return null;
        }

        var group = members[spine];

        var touchesPage = group.OfType<PageEngineEvent>().Any(m => m.PageAddress == page);

        if (!touchesPage)
        {
            return null;
        }

        var start = group.Min(m => m.TimeUs);

        var end = group.Max(m => m.TimeUs + m.DurationUs);

        return latch.TimeUs >= start && latch.TimeUs <= end + CachedFoldToleranceUs ? spine : null;
    }

    private const long UnmeasuredReadUs = 1_000;

    private static void EstimateGatherDurations(Dictionary<EngineEvent, List<EngineEvent>> members)
    {
        foreach (var spine in members.Keys.OfType<FileEvent>())
        {
            if (spine.DurationUs <= 0)
            {
                spine.DurationUs = UnmeasuredReadUs;
            }
        }
    }

    private static LatchEvent? ResolveSuspend(EngineEvent e,
                                              Dictionary<ulong, List<LatchEvent>> byLatchAddress,
                                              Dictionary<PageAddress, List<LatchEvent>> byPage)
    {
        return e switch
        {
            WaitEvent { WaitResource: { } resource } => Nearest(byLatchAddress, resource, e.TimeUs),
            LatchEvent { LatchAddress: { } address } => Nearest(byLatchAddress, address, e.TimeUs),
            IoEvent { PageAddress: { } page } => Nearest(byPage, page, e.TimeUs),
            FileEvent { PageAddress: { } page } => Nearest(byPage, page, e.TimeUs),
            _ => null,
        };
    }

    private const long ToleranceUs = 1_000;

    private static LatchEvent? Nearest<TKey>(Dictionary<TKey, List<LatchEvent>> index, TKey key, long timeUs)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var candidates))
        {
            return null;
        }

        foreach (var spine in candidates)
        {
            if (timeUs >= spine.TimeUs && timeUs <= spine.TimeUs + spine.DurationUs)
            {
                return spine;
            }
        }

        var nearest = candidates.MinBy(s => WindowDistance(s, timeUs));

        return nearest is not null && WindowDistance(nearest, timeUs) <= ToleranceUs ? nearest : null;
    }

    private static long WindowDistance(LatchEvent spine, long timeUs)
    {
        if (timeUs < spine.TimeUs)
        {
            return spine.TimeUs - timeUs;
        }

        var end = spine.TimeUs + spine.DurationUs;

        return timeUs > end ? timeUs - end : 0;
    }

    private static FileEvent? ContainingGather(List<FileEvent> spines, PageAddress page, long timeUs)
    {
        FileEvent? best = null;

        var bestDistance = long.MaxValue;

        foreach (var spine in spines)
        {
            var from = spine.FromPageAddress;

            if (page.FileId != from.FileId
                || page.PageId < from.PageId
                || page.PageId >= spine.ToPageAddress.PageId)
            {
                continue;
            }

            var distance = Math.Abs(spine.TimeUs - timeUs);

            if (distance < bestDistance)
            {
                best = spine;

                bestDistance = distance;
            }
        }

        return best;
    }

    private static ReadEventGroup BuildGroup(EngineEvent spine, List<EngineEvent> members)
    {
        var kind = spine is FileEvent || members.Any(IsFileReadMarker) ? ReadType.NonCached : ReadType.Cached;

        var completion = spine.TimeUs + spine.DurationUs;

        foreach (var member in members)
        {
            if (member is IoEvent { IsRead: true })
            {
                member.TimeUs = completion;
            }
        }

        var ordered = members.OrderBy(m => m.TimeUs).ToList();

        var identity = ordered.FirstOrDefault(m => !string.IsNullOrEmpty(m.TableName)) ?? spine;

        var planHandleId = ordered.Select(m => m.PlanHandleId).FirstOrDefault(h => h != 0);

        var pages = ordered.OfType<PageEngineEvent>()
                           .Where(m => m.PageAddress is not null)
                           .Select(m => m.PageAddress!.Value)
                           .Distinct()
                           .OrderBy(p => p.FileId)
                           .ThenBy(p => p.PageId)
                           .ToList();

        return new ReadEventGroup
        {
            Name = "Page Read",
            Events = ordered,
            ReadType = kind,
            Pages = pages,
            SequenceId = spine.SequenceId,
            TimeUs = spine.TimeUs,
            DurationUs = spine.DurationUs,
            DatabaseId = spine.DatabaseId,
            Timestamp = spine.Timestamp,
            Category = EventCategory.Io,
            TaskAddress = spine.TaskAddress,
            WorkerAddress = spine.WorkerAddress,
            PlanHandleId = planHandleId,
            AllocationUnit = identity.AllocationUnit,
        };
    }

    private static bool IsFileReadMarker(EngineEvent e) => e switch
    {
        IoEvent { IsRead: true } 
            => true,
        WaitEvent w => w.WaitType.IsPageIoLatchWait(),
        _ => false,
    };

    private static bool IsSuspend(LatchEvent e) => e.Name == "latch_suspend_begin";

    private static bool IsGatherMember(EngineEvent e) => e switch
    {
        IoEvent { IsRead: true } => true,
        LatchEvent { LatchClass: LatchClass.BUF, LatchMode: LatchMode.EX or LatchMode.KP } => true,
        _ => false,
    };

    private static PageAddress? PageOf(EngineEvent e) => e switch
    {
        IoEvent io => io.PageAddress,
        LatchEvent latch => latch.PageAddress,
        FileEvent file => file.PageAddress,
        _ => null,
    };

    private static bool IsCachedBufferAcquire(LatchEvent e) =>
        e is { LatchClass: LatchClass.BUF, LatchMode: LatchMode.SH, PageAddress: not null, Name: "latch_acquired" };

    private static int DistinctPageCount(List<EngineEvent> group) =>
        group.OfType<PageEngineEvent>()
             .Where(m => m.PageAddress is not null)
             .Select(m => m.PageAddress!.Value)
             .Distinct()
             .Take(2)
             .Count();
}
