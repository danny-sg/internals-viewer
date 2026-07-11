using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Locks;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Second consolidation step that binds the storage events of a page read into a single NonCachedReadEventGroup
/// </summary>
/// <remarks>
/// SQL Server reads pages three ways, each with its own spine (the event that anchors one read):
///
/// - Contiguous single-page reads (typically clustered system tables) block the worker, so they carry a
///   <c>latch_suspend</c> + <c>PAGEIOLATCH</c> wait whose folded duration is the real microsecond read time. The
///   suspend is the spine; storage events attach by buffer latch address or page.
/// - Scatter/Gather reads pull a whole page range in one physical I/O and never suspend. The <c>file_read</c> (folded
///   from its begin/completed pair, carrying the range in <c>Size</c>) is the spine; <c>physical_page_read</c>s and BUF
///   latches attach by page-in-range. Nothing here is measured, so durations are estimated from bytes over the read
///   phase (see <see cref="EstimateGatherDurations"/>).
/// - Cached reads never touch disk: a bare BUF SH latch on a page the two cold passes did not consume.
///
/// Members link to a spine by address, page, or range rather than capture order, which mixes begins and ends.
/// </remarks>
public static class ColdReadGrouper
{
    public static List<EngineEvent> Group(IReadOnlyList<EngineEvent> events)
    {
        // Keyed by reference: EngineEvent records compare by value, so value-equal events would otherwise collide. Each
        // spine (a suspend, a gather file read, or a cached acquire) maps to the storage events that make up that read.
        var members = new Dictionary<EngineEvent, List<EngineEvent>>(ReferenceEqualityComparer.Instance);

        var consumed = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        CollectContiguousColdReads(events, members, consumed);

        CollectGatherReads(events, members, consumed);

        CollectCachedReads(events, members, consumed);

        EstimateGatherDurations(events, members);

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

    // Contiguous single-page cold read: the folded latch suspend is the spine (its wait duration is the real read
    // time) and the wait, physical read, file read and BUF latches attach by buffer latch address or page.
    private static void CollectContiguousColdReads(
        IReadOnlyList<EngineEvent> events,
        Dictionary<EngineEvent, List<EngineEvent>> members,
        HashSet<EngineEvent> consumed)
    {
        var spines = events.OfType<LatchEvent>().Where(IsSuspend).ToList();

        if (spines.Count == 0)
        {
            return;
        }

        var byLatchAddress = spines
            .Where(s => s.LatchAddress is not null)
            .GroupBy(s => s.LatchAddress!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.TimeUs).ToList());

        var byPage = spines
            .Where(s => s.PageAddress is not null)
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

    // File-read-spined cold read: a completed file read (Size gives the page range — one page for Contiguous, many for
    // Scatter/Gather) that the suspend pass did not consume. The physical reads and EX BUF latches for pages in its
    // range attach. Catches Scatter/Gather reads (which never suspend) and Contiguous reads that finished without
    // suspending (fast I/O), so a single-page read with no suspend no longer falls through ungrouped.
    private static void CollectGatherReads(
        IReadOnlyList<EngineEvent> events,
        Dictionary<EngineEvent, List<EngineEvent>> members,
        HashSet<EngineEvent> consumed)
    {
        var spines = events
            .OfType<FileEvent>()
            .Where(f => f.Size > 0 && f.PageAddress is not null && !consumed.Contains(f) && !members.ContainsKey(f))
            .ToList();

        if (spines.Count == 0)
        {
            return;
        }

        foreach (var spine in spines)
        {
            members[spine] = [spine];
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

            members[spine].Add(e);

            consumed.Add(e);
        }
    }

    // Cached read: a page already in the buffer pool, read under a bare BUF SH latch with no load. Any such acquire the
    // cold passes did not consume is its own single-page cached read (its folded release supplies the hold duration).
    private static void CollectCachedReads(
        IReadOnlyList<EngineEvent> events,
        Dictionary<EngineEvent, List<EngineEvent>> members,
        HashSet<EngineEvent> consumed)
    {
        foreach (var e in events)
        {
            if (e is LatchEvent latch
                && !consumed.Contains(latch)
                && !members.ContainsKey(latch)
                && IsCachedBufferAcquire(latch))
            {
                members[latch] = [latch];
            }
        }
    }

    // Gather reads carry no measured duration, so estimate one: the read phase's wall time (span of the read events)
    // is shared out across the gather reads in proportion to the pages each moved, per the average byte-rate model.
    private static void EstimateGatherDurations(
        IReadOnlyList<EngineEvent> events,
        Dictionary<EngineEvent, List<EngineEvent>> members)
    {
        var spines = members.Keys.OfType<FileEvent>().ToList();

        if (spines.Count == 0)
        {
            return;
        }

        var totalPages = spines.Sum(PageCountOf);

        if (totalPages <= 0)
        {
            return;
        }

        var reads = events.Where(e => e is IoEvent { IsRead: true } or FileEvent).ToList();

        var span = reads.Count == 0 ? 0 : reads.Max(e => e.TimeUs) - reads.Min(e => e.TimeUs);

        foreach (var spine in spines)
        {
            spine.DurationUs = span * PageCountOf(spine) / totalPages;
        }
    }

    private static LatchEvent? ResolveSuspend(
        EngineEvent e,
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

    // Quantization slop: capture timestamps round to the millisecond, so a member landing just outside a spine's
    // window still belongs to it. The nearest-in-time fall back only applies within this bound so a point window
    // (an unpaired suspend that kept DurationUs 0) cannot swallow events from elsewhere in the query.
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

    // Gap between a time and a spine's [TimeUs, TimeUs + DurationUs] window, zero when the time falls inside it.
    private static long WindowDistance(LatchEvent spine, long timeUs)
    {
        if (timeUs < spine.TimeUs)
        {
            return spine.TimeUs - timeUs;
        }

        var end = spine.TimeUs + spine.DurationUs;

        return timeUs > end ? timeUs - end : 0;
    }

    // The gather read whose page range covers the page, nearest in time when more than one range does.
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

    private static NonCachedReadEventGroup BuildGroup(EngineEvent spine, List<EngineEvent> members)
    {
        // A gather file read is itself a physical I/O, so its group is non-cached regardless of which members landed;
        // a suspend/cached group is non-cached only if a cold marker (a physical read or PAGEIOLATCH wait) attached.
        var kind = spine is FileEvent || members.Any(IsColdMarker) ? ReadKind.NonCached : ReadKind.Cached;

        // The spine (suspend or gather file read) carries no table identity itself, so it is taken from whichever
        // member resolved it (a physical read or latch, enriched with the allocation unit), falling back to the spine.
        var identity = members.FirstOrDefault(m => !string.IsNullOrEmpty(m.TableName)) ?? spine;

        // The plan handle links the group back to its execution plan, so it is preserved from any member
        // that carries one (0 is the "no handle" sentinel).
        var planHandleId = members.Select(m => m.PlanHandleId).FirstOrDefault(h => h != 0);

        // Every distinct page the read touched (physical reads and BUF latches), so the group links to all of them
        // rather than a single page — the spine's own page is included as it is itself a member.
        var pages = members
            .OfType<PageEngineEvent>()
            .Where(m => m.PageAddress is not null)
            .Select(m => m.PageAddress!.Value)
            .Distinct()
            .OrderBy(p => p.FileId)
            .ThenBy(p => p.PageId)
            .ToList();

        return new NonCachedReadEventGroup
        {
            Name = "page_read",
            Events = members,
            Kind = kind,
            Pages = pages,
            // Timing comes from the spine: a suspend's folded DurationUs is the SQL-measured read time, a gather's is
            // the estimated one; a min/max envelope over the children would only reflect their ms-quantised timestamps.
            TimeUs = spine.TimeUs,
            DurationUs = spine.DurationUs,
            DatabaseId = spine.DatabaseId,
            Timestamp = spine.Timestamp,
            Category = EventCategory.Io,
            TaskAddress = spine.TaskAddress,
            WorkerAddress = spine.WorkerAddress,
            PlanHandleId = planHandleId,
            ObjectId = identity.ObjectId,
            ObjectName = identity.ObjectName,
            SchemaName = identity.SchemaName,
            TableName = identity.TableName,
            IndexName = identity.IndexName,
        };
    }

    private static bool IsColdMarker(EngineEvent e) => e switch
    {
        IoEvent { IsRead: true } => true,
        WaitEvent w => w.WaitType.IsPageIoLatchWait(),
        _ => false,
    };

    private static bool IsSuspend(LatchEvent e) => e.Name == "latch_suspend_begin";

    // Pages in a gather read's range; at least one so a range read never estimates to zero pages.
    private static int PageCountOf(FileEvent f) => Math.Max(1, f.ToPageAddress.PageId - f.FromPageAddress.PageId);

    // Events that make up a gather read: the physical page reads and the paired EX BUF latches that load each page
    // into a frame. Together with the file read they are the non-cached signature. SH latches (the later scan read of
    // the now-resident page) are NOT part of the load and are left out.
    private static bool IsGatherMember(EngineEvent e) => e switch
    {
        IoEvent { IsRead: true } => true,
        LatchEvent { LatchClass: LatchClass.BUF, LatchMode: LatchMode.EX } => true,
        _ => false,
    };

    private static PageAddress? PageOf(EngineEvent e) => e switch
    {
        IoEvent io => io.PageAddress,
        LatchEvent latch => latch.PageAddress,
        FileEvent file => file.PageAddress,
        _ => null,
    };

    // A shared buffer latch acquire on a page: the fingerprint of a cached read (a page read straight from the buffer
    // pool with no load). Its release has already been folded in, so this single event carries the hold duration.
    private static bool IsCachedBufferAcquire(LatchEvent e) =>
        e is { LatchClass: LatchClass.BUF, LatchMode: LatchMode.SH, PageAddress: not null, Name: "latch_acquired" };
}
