using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Locators.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public sealed class TracePageAccessList
{
    private const int ThisRegister = 0;

    private TracePageAccessList(TracePageAccess[] accesses, TracePageHeaderAccess[] headerAccesses)
    {
        Accesses = accesses;

        HeaderAccesses = headerAccesses;
    }

    public static TracePageAccessList Empty { get; } = new([], []);

    public int Count => Accesses.Length;

    public TracePageHeaderAccess[] HeaderAccesses { get; }

    private TracePageAccess[] Accesses { get; }

    public TracePageAccess this[int index] => Accesses[index];

    public TracePageAccess? GetFetch(TracePosition position) => Find(position, isRow: false);

    public TracePageAccess? GetRowAccess(TracePosition position) => Find(position, isRow: true);

    public TracePageHeaderAccess? GetHeaderAccess(TracePosition position)
    {
        var index = SortedSearch.FirstAtOrAfter(HeaderAccesses.AsSpan(), position, a => a.Access.Position);

        return index < HeaderAccesses.Length && HeaderAccesses[index].Access.Position == position ? HeaderAccesses[index] : null;
    }

    public double? GetSlotFraction(TracePosition position, TraceBufferPool pool)
    {
        if (GetRowAccess(position) is not { Offset: { } offset } access
            || pool.GetLayout(access.Page, access.DatabaseId) is not { } layout
            || layout.GetSlot(offset) is not { } slot)
        {
            return null;
        }

        var count = layout.OffsetTable.Length;

        return count > 1 ? (double)slot / (count - 1) : 0;
    }

    public static TracePageAccessList Build(TraceCallStore calls,
                                            MemoryHistoryStore history,
                                            TraceBufferPool pool,
                                            TracePageReadList? reads = null,
                                            TraceTimeline? timeline = null)
    {
        var fetches = GetFetches(calls, pool);

        var rows = reads is { Count: > 0 } ? GetReadRows(calls, reads, pool, timeline) : GetPointerRows(calls, history, pool);

        var ordered = new List<TracePageAccess>();

        var headers = new List<TracePageHeaderAccess>();

        var filter = new SnapshotFilter();

        var previous = new Dictionary<uint, TracePageAccess>();

        var openHeaders = new Dictionary<uint, int>();

        var nextFetch = 0;

        var nextRow = 0;

        while (nextFetch < fetches.Length || nextRow < rows.Count)
        {
            var candidate = nextRow == rows.Count
                            || (nextFetch < fetches.Length && fetches[nextFetch].Access.Position <= rows[nextRow].Access.Position)
                ? fetches[nextFetch++]
                : rows[nextRow++];

            if (candidate.Access.Offset < PageHeader.Size)
            {
                AddHeaderAccess(headers, openHeaders, candidate);

                continue;
            }

            if (GetRow(pool, candidate) is not { } row || !filter.IsAccess(row) || IsRepeat(previous, row))
            {
                continue;
            }

            openHeaders.Remove(row.Thread);

            ordered.Add(row.Access);
        }

        return new TracePageAccessList([.. ordered], [.. headers]);
    }

    private TracePageAccess? Find(TracePosition position, bool isRow)
    {
        for (var index = SortedSearch.FirstAtOrAfter(Accesses.AsSpan(), position, a => a.Position);
             index < Accesses.Length && Accesses[index].Position == position;
             index++)
        {
            if ((Accesses[index].Offset is not null) == isRow)
            {
                return Accesses[index];
            }
        }

        return null;
    }

    private static Candidate[] GetFetches(TraceCallStore calls, TraceBufferPool pool)
    {
        List<TraceCallList> lists = [.. calls.GetCallLists().Where(l => IsFetch(calls, l))];

        var candidates = new List<Candidate>(lists.Sum(l => l.Count));

        foreach (var list in lists)
        {
            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                var buffer = call.Returned ? pool.GetReference(call.ReturnValue, call.ToPosition) : null;

                if ((GetPage(call) ?? buffer?.Page) is { } page)
                {
                    var access = new TracePageAccess(call.FromPosition, page, buffer?.DatabaseId ?? 0, null, call.CallNodeIndex, index);

                    candidates.Add(new Candidate(access, call.ThreadId, false));
                }
            }
        }

        return [.. candidates.OrderBy(c => c.Access.Position)];
    }

    private static bool IsFetch(TraceCallStore calls, TraceCallList list)
        => calls.GetFirstNode(list)?.Frame is { Function.MethodName: BufferPoolFunctionLookup.GetMethod } frame
           && BufferPoolFunctionLookup.IsBufferPool(frame);

    private static List<Candidate> GetReadRows(TraceCallStore calls, TracePageReadList reads, TraceBufferPool pool, TraceTimeline? timeline)
    {
        var candidates = new List<Candidate>(reads.Count);

        AddReads(reads, pool, timeline, GetPostReads(calls), candidates);

        return IsSorted(candidates) ? candidates : [.. candidates.OrderBy(c => c.Access.Position)];
    }

    private static bool IsSorted(List<Candidate> candidates)
    {
        for (var index = 1; index < candidates.Count; index++)
        {
            if (candidates[index].Access.Position < candidates[index - 1].Access.Position)
            {
                return false;
            }
        }

        return true;
    }

    private static List<Candidate> GetPointerRows(TraceCallStore calls, MemoryHistoryStore history, TraceBufferPool pool)
    {
        var candidates = new List<Candidate>();

        AddRecordCalls(calls, pool, candidates);

        AddWrittenPointers(history, pool, candidates);

        return [.. candidates.OrderBy(c => c.Access.Position)];
    }

    private static Dictionary<uint, List<(TracePosition From, TracePosition To)>> GetPostReads(TraceCallStore calls)
    {
        var postReads = new Dictionary<uint, List<(TracePosition From, TracePosition To)>>();

        foreach (var list in calls.GetCallLists())
        {
            if (!BufferPoolFunctionLookup.IsPostRead(calls.GetFirstNode(list)?.Frame))
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                ref var spans = ref CollectionsMarshal.GetValueRefOrAddDefault(postReads, call.ThreadId, out _);

                (spans ??= []).Add((call.FromPosition, call.ToPosition));
            }
        }

        foreach (var spans in postReads.Values)
        {
            spans.Sort(static (left, right) => left.From.CompareTo(right.From));
        }

        return postReads;
    }

    private static bool IsPostRead(Dictionary<uint, List<(TracePosition From, TracePosition To)>> postReads,
                                   uint thread,
                                   TracePosition position)
    {
        if (!postReads.TryGetValue(thread, out var spans))
        {
            return false;
        }

        var index = SortedSearch.FirstAfter(CollectionsMarshal.AsSpan(spans), position, s => s.From) - 1;

        return index >= 0 && position <= spans[index].To;
    }

    private static void AddReads(TracePageReadList reads,
                                 TraceBufferPool pool,
                                 TraceTimeline? timeline,
                                 Dictionary<uint, List<(TracePosition From, TracePosition To)>> postReads,
                                 List<Candidate> candidates)
    {
        var calls = new TraceInnermostCallIndex(timeline);

        for (var index = 0; index < reads.Count; index++)
        {
            var read = reads[index];

            if (pool.GetFrameReference(read.Address, read.Position) is not { } reference)
            {
                continue;
            }

            var isPostRead = IsPostRead(postReads, read.Thread, read.Position);

            var isWholePage = isPostRead || (reference.Offset == PageHeader.Size && reference.Offset + read.Length == PageData.Size);

            var hasLayout = pool.GetLayout(reference.Page, reference.DatabaseId) is not null;

            if (reference.Offset < PageHeader.Size ? isPostRead : !isWholePage && !hasLayout)
            {
                continue;
            }

            var (callNodeIndex, callIndex) = calls.GetCall(read.Thread, read.Position);

            var access = new TracePageAccess(read.Position,
                                             reference.Page,
                                             reference.DatabaseId,
                                             isWholePage ? null : reference.Offset,
                                             callNodeIndex,
                                             callIndex);

            candidates.Add(new Candidate(access, read.Thread, false, read.Length));
        }
    }

    private static void AddHeaderAccess(List<TracePageHeaderAccess> headers, Dictionary<uint, int> openHeaders, Candidate candidate)
    {
        var access = candidate.Access;

        var bytes = TracePageHeaderAccess.GetBytes(access.Offset ?? 0, candidate.Length);

        if (openHeaders.TryGetValue(candidate.Thread, out var index)
            && headers[index].Access.Page == access.Page
            && headers[index].Access.DatabaseId == access.DatabaseId
            && headers[index].Access.CallNodeIndex == access.CallNodeIndex
            && headers[index].Access.CallIndex == access.CallIndex)
        {
            headers[index] = headers[index] with { Bytes = headers[index].Bytes | bytes };

            return;
        }

        openHeaders[candidate.Thread] = headers.Count;

        headers.Add(new TracePageHeaderAccess(access, bytes));
    }

    private static void AddRecordCalls(TraceCallStore calls, TraceBufferPool pool, List<Candidate> candidates)
    {
        var buffer = new TraceCallStore.ColumnBuffer();

        foreach (var list in calls.GetCallLists())
        {
            if (calls.GetFirstNode(list)?.Frame is not { Function.ClassName.Length: > 0 } frame
                || !frame.Module.Name.Equals(BufferPoolFunctionLookup.Module, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var (columns, first) in list.ReadChunks(buffer))
            {
                for (var callIndex = 0; callIndex < columns.CallCount; callIndex++)
                {
                    var position = columns.GetFromPosition(callIndex);

                    var address = columns.GetRegister(callIndex, ThisRegister);

                    if (GetRecord(pool, address, position, columns.GetCallNodeIndex(callIndex), first + callIndex) is { } access)
                    {
                        candidates.Add(new Candidate(access, columns.GetThread(callIndex), false));
                    }
                }
            }
        }
    }

    private static void AddWrittenPointers(MemoryHistoryStore history, TraceBufferPool pool, List<Candidate> candidates)
    {
        for (var block = 0; block < history.Blocks.Count; block++)
        {
            var item = history.Blocks[block];

            foreach (var (write, value) in MemoryHistoryPointerReader.GetWrittenPointers(history, block))
            {
                if (GetRecord(pool, value, write.Position, write.CallNodeIndex, write.CallIndex) is { } access)
                {
                    var isSnapshot = item.IsStack && write.Type == MemoryHistoryAccessType.Initialised;

                    candidates.Add(new Candidate(access, item.Thread, isSnapshot));
                }
            }
        }
    }

    private static TracePageAccess? GetRecord(TraceBufferPool pool,
                                              ulong address,
                                              TracePosition position,
                                              int callNodeIndex,
                                              int callIndex)
        => address != 0 && pool.GetFrameReference(address, position) is { } reference && reference.Offset >= PageHeader.Size
            ? new TracePageAccess(position, reference.Page, reference.DatabaseId, reference.Offset, callNodeIndex, callIndex)
            : null;

    private static PageAddress? GetPage(TraceArgumentCall call)
    {
        foreach (var locator in call.Locators)
        {
            if (locator.ValueType == LocatorValueType.Page)
            {
                return locator.Page;
            }
        }

        return null;
    }

    private static Candidate? GetRow(TraceBufferPool pool, Candidate candidate)
    {
        var access = candidate.Access;

        if (access.Offset is not { } offset || pool.GetLayout(access.Page, access.DatabaseId) is not { } layout)
        {
            return candidate;
        }

        if (!HasRows(layout) || IsOffsetTable(layout, offset))
        {
            return null;
        }

        return layout.GetSlot(offset) is { } slot && layout.OffsetTable[slot] >= PageHeader.Size
            ? candidate with { Access = access with { Offset = layout.OffsetTable[slot] } }
            : null;
    }

    private static bool HasRows(PageData layout) => layout.PageHeader.PageType is PageType.None or PageType.Data or PageType.Index;

    private static bool IsOffsetTable(PageData layout, int offset) => offset >= PageData.Size - layout.OffsetTable.Length * sizeof(ushort);

    private static bool IsRepeat(Dictionary<uint, TracePageAccess> previous, Candidate candidate)
    {
        var access = candidate.Access;

        var isRepeat = previous.TryGetValue(candidate.Thread, out var last)
                       && last.Page == access.Page
                       && last.DatabaseId == access.DatabaseId
                       && last.Offset == access.Offset;

        previous[candidate.Thread] = access;

        return isRepeat;
    }

    private readonly record struct Candidate(TracePageAccess Access, uint Thread, bool IsSnapshot, int Length = 0);

    private sealed class SnapshotFilter
    {
        private Dictionary<uint, (PageAddress Page, ushort DatabaseId)> LastFetches { get; } = [];

        private Dictionary<(PageAddress Page, ushort DatabaseId), HashSet<int>> Rows { get; } = [];

        public bool IsAccess(Candidate candidate)
        {
            var page = (candidate.Access.Page, candidate.Access.DatabaseId);

            if (candidate.Access.Offset is not { } offset)
            {
                LastFetches[candidate.Thread] = page;

                Rows[page] = [];

                return true;
            }

            ref var rows = ref CollectionsMarshal.GetValueRefOrAddDefault(Rows, page, out _);

            rows ??= [];

            var isNew = rows.Add(offset);

            return !candidate.IsSnapshot || (isNew && LastFetches.TryGetValue(candidate.Thread, out var fetched) && fetched == page);
        }
    }
}
