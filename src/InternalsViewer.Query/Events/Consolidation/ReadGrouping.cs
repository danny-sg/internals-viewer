using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Waits;

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
public static class ReadGrouping
{
    public static List<EngineEvent> Group(IReadOnlyList<EngineEvent> events)
    {
        // Keyed by reference: EngineEvent records compare by value, so value-equal events would otherwise collide. Each
        // spine (a suspend, a gather file read, or a cached acquire) maps to the storage events that make up that read.
        var members = new Dictionary<EngineEvent, List<EngineEvent>>(ReferenceEqualityComparer.Instance);

        var consumed = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        CollectContiguousNonCachedReads(events, members, consumed);

        CollectGatherReads(events, members, consumed);

        CollectCachedReads(events, members, consumed);

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

    private static void CollectContiguousNonCachedReads(IReadOnlyList<EngineEvent> events,
                                                        Dictionary<EngineEvent, List<EngineEvent>> members,
                                                        HashSet<EngineEvent> consumed)
    {
        var spines = events.OfType<LatchEvent>().Where(IsSuspend).ToList();

        if (spines.Count == 0)
        {
            return;
        }

        var byLatchAddress = spines.Where(s => s.LatchAddress is not null)
                                   .GroupBy(s => s.LatchAddress!.Value)
                                   .ToDictionary(g => g.Key, g => g.OrderBy(s => s.TimeUs).ToList());

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

    private static void CollectGatherReads(IReadOnlyList<EngineEvent> events,
                                           Dictionary<EngineEvent, List<EngineEvent>> members,
                                           HashSet<EngineEvent> consumed)
    {
        var spines = events
            .OfType<FileEvent>()
            .Where(f => f is { Size: > 0, PageAddress: not null } && !consumed.Contains(f) && !members.ContainsKey(f))
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

    private static void CollectCachedReads(IReadOnlyList<EngineEvent> events,
                                           Dictionary<EngineEvent, List<EngineEvent>> members,
                                           HashSet<EngineEvent> consumed)
    {
        var nonCachedByAddress = new Dictionary<ulong, EngineEvent>();

        foreach (var (spine, group) in members)
        {
            if (spine is not FileEvent && !group.Any(IsColdMarker))
            {
                continue;
            }

            foreach (var member in group)
            {
                if (member is LatchEvent { LatchAddress: { } address })
                {
                    nonCachedByAddress[address] = spine;
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

            if (FoldTarget(latch, members, nonCachedByAddress) is { } spine)
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
        var kind = spine is FileEvent || members.Any(IsColdMarker) ? ReadType.NonCached : ReadType.Cached;

        var identity = members.FirstOrDefault(m => !string.IsNullOrEmpty(m.TableName)) ?? spine;

        var planHandleId = members.Select(m => m.PlanHandleId).FirstOrDefault(h => h != 0);

        var pages = members.OfType<PageEngineEvent>()
                           .Where(m => m.PageAddress is not null)
                           .Select(m => m.PageAddress!.Value)
                           .Distinct()
                           .OrderBy(p => p.FileId)
                           .ThenBy(p => p.PageId)
                           .ToList();

        return new ReadEventGroup
        {
            Name = "Page Read",
            Events = members,
            ReadType = kind,
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
            AllocationUnit = identity.AllocationUnit,
        };
    }

    private static bool IsColdMarker(EngineEvent e) => e switch
    {
        IoEvent { IsRead: true } => true,
        WaitEvent w => w.WaitType.IsPageIoLatchWait(),
        _ => false,
    };

    private static bool IsSuspend(LatchEvent e) => e.Name == "latch_suspend_begin";

    private static int PageCountOf(FileEvent f) => Math.Max(1, f.ToPageAddress.PageId - f.FromPageAddress.PageId);

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
}
