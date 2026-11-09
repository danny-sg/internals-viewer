using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimeline
{
    public const string ShutdownSymbol = "CXStmtQuery::ShutdownNormal";

    internal TraceTimeline(IReadOnlyList<TraceTimelineThread> threads, int[] parents, TraceScale scale)
    {
        Threads = threads;
        Parents = parents;
        Scale = scale;
        SpanCount = threads.Sum(t => t.CallCount);

        if (threads.Count == 0)
        {
            return;
        }

        Start = new TracePoint(threads.Min(t => t.Start.Position), 0);

        End = new TracePoint(threads.Max(t => t.End.Position), threads.Max(t => t.End.Instructions));
    }

    private TraceTimeline(IReadOnlyList<TraceTimelineThread> threads, TraceTimeline source, bool fit = false)
    {
        Threads = threads;
        Parents = source.Parents;
        Nodes = source.Nodes;
        Scale = source.Scale;
        SpanCount = threads.Sum(t => t.CallCount);
        Start = source.Start;
        End = source.End;
        Calls = source.Calls;

        if (!fit || threads.Count == 0)
        {
            return;
        }

        Start = new TracePoint(threads.Min(t => t.Start.Position), threads.Min(t => t.Start.Instructions));
        End = new TracePoint(threads.Max(t => t.End.Position), threads.Max(t => t.End.Instructions));
    }

    public IReadOnlyList<TraceTimelineThread> Threads { get; }

    public TraceScale Scale { get; }

    public TracePoint Start { get; private set; }

    public TracePoint End { get; private set; }

    public int NodeCount => Parents.Length;

    public long SpanCount { get; private set; }

    internal long Bytes => Threads.Sum(t => t.Bytes) + Parents.Length * (long)sizeof(int) + Nodes.Length * (long)IntPtr.Size;

    internal CallTreeNode?[] MappedNodes => Nodes;

    private int[] Parents { get; }

    private CallTreeNode?[] Nodes { get; set; } = [];

    private TraceCallStore? Calls { get; set; }

    private TraceSpanIdIndex? SpanIds { get; set; }

    private double[]? InstructionCuts { get; set; }

    public IReadOnlyList<double> GetCuts(TraceTimelineAxis axis)
        => axis == TraceTimelineAxis.Position
            ? Scale.Cuts
            : InstructionCuts ??= [.. Threads.SelectMany(t => t.Clock.GetDeadSpans().Select(s => (double)t.Clock.GetInstructions(s.From)))
                                             .Distinct()
                                             .Order()];

    public double GetStart(TraceTimelineAxis axis) => Scale.GetValue(Start, axis);

    public double GetEnd(TraceTimelineAxis axis) => Scale.GetValue(End, axis);

    public TracePoint GetPoint(TracePosition position)
        => new(position, Threads.Select(t => t.Clock.GetInstructions(position)).DefaultIfEmpty().Max());

    public CallTreeNode? NodeOf(int callNodeIndex) => callNodeIndex >= 0 && callNodeIndex < Nodes.Length ? Nodes[callNodeIndex] : null;

    public TraceSpanLocation? FindCall(CallTreeNode node, int callIndex)
        => node.Frame is { } frame ? FindCall(frame.Address, frame.InstanceAddress, callIndex) : null;

    public TraceSpanLocation? FindCall(ulong functionAddress, ulong instanceAddress, int callIndex)
    {
        if (Calls?.GetCallList(functionAddress, instanceAddress) is not { } calls || callIndex < 0 || callIndex >= calls.Count)
        {
            return null;
        }

        return Find(calls.GetThread(callIndex), calls.GetFromPosition(callIndex), calls.GetCallNodeIndex(callIndex), callIndex);
    }

    public TraceSpanLocation? FindSpan(uint thread, TraceSpan span) => Find(thread, span.From.Position, span.CallNodeIndex, span.CallIndex);

    private TraceSpanLocation? Find(uint thread, TracePosition from, int callNodeIndex, int callIndex)
    {
        if (NodeOf(callNodeIndex) is not { } callNode)
        {
            return null;
        }

        var depth = DepthOf(callNodeIndex);

        for (var lane = 0; lane < Threads.Count; lane++)
        {
            if (Threads[lane].ThreadId != thread)
            {
                continue;
            }

            var rows = Threads[lane].Rows;

            if (depth < rows.Count && rows[depth].GetIndex(from, callNodeIndex, callIndex) is >= 0 and var expected)
            {
                return new TraceSpanLocation(lane, Threads[lane], depth, rows[depth], expected, callNode);
            }

            for (var row = 0; row < rows.Count; row++)
            {
                if (row != depth && rows[row].GetIndex(from, callNodeIndex, callIndex) is >= 0 and var index)
                {
                    return new TraceSpanLocation(lane, Threads[lane], row, rows[row], index, callNode);
                }
            }
        }

        return null;
    }

    public TraceSpan? GetInnermostCall(uint thread, TracePosition position)
    {
        for (var laneIndex = 0; laneIndex < Threads.Count; laneIndex++)
        {
            var lane = Threads[laneIndex];

            if (lane.ThreadId != thread)
            {
                continue;
            }

            for (var row = lane.Rows.Count - 1; row >= 0; row--)
            {
                if (lane.Rows[row].GetIndex(position) is >= 0 and var index)
                {
                    return lane.Rows[row].GetSpan(index);
                }
            }
        }

        return null;
    }

    public IReadOnlyList<TraceThreadStack> GetStack(TracePosition position)
    {
        var stacks = new List<TraceThreadStack>();

        foreach (var thread in Threads)
        {
            var calls = new List<TraceSpan>();

            foreach (var row in thread.Rows)
            {
                var index = row.GetIndex(position);

                if (index >= 0)
                {
                    calls.Add(row.GetSpan(index));
                }
            }

            if (calls.Count > 0)
            {
                stacks.Add(new TraceThreadStack(thread.ThreadId, calls));
            }
        }

        return stacks;
    }

    public IEnumerable<TraceSpanLocation> GetSpanLocations()
    {
        for (var threadIndex = 0; threadIndex < Threads.Count; threadIndex++)
        {
            var thread = Threads[threadIndex];

            for (var depth = 0; depth < thread.Rows.Count; depth++)
            {
                var row = thread.Rows[depth];

                for (var index = 0; index < row.Count; index++)
                {
                    if (NodeOf(row.GetCallNodeIndex(index)) is { } node)
                    {
                        yield return new TraceSpanLocation(threadIndex, thread, depth, row, index, node);
                    }
                }
            }
        }
    }

    public IReadOnlyList<TraceSelfAllocation> GetSelfAllocations(AllocationIndex allocation)
    {
        var totals = new Dictionary<(int Thread, int Depth, int Index), ulong>();

        for (var thread = 0; thread < Threads.Count; thread++)
        {
            var rows = Threads[thread].Rows;

            foreach (var (position, bytes) in allocation.GetAllocations(Threads[thread].ThreadId))
            {
                for (var depth = rows.Count - 1; depth >= 0; depth--)
                {
                    var index = rows[depth].GetIndex(position);

                    if (index < 0)
                    {
                        continue;
                    }

                    var key = (thread, depth, index);

                    totals[key] = totals.GetValueOrDefault(key) + bytes;

                    break;
                }
            }
        }

        return [.. totals.OrderBy(t => t.Key).Select(t => new TraceSelfAllocation(t.Key.Thread, t.Key.Depth, t.Key.Index, t.Value))];
    }

    public int ParentOf(int callNodeIndex) => callNodeIndex >= 0 && callNodeIndex < Parents.Length ? Parents[callNodeIndex] : -1;

    public (TraceSpan Span, uint ThreadId)? GetFirstRootCall()
    {
        (TraceSpan Span, uint ThreadId)? first = null;

        foreach (var thread in Threads.Where(t => t.Rows.Count > 0 && t.Rows[0].Count > 0))
        {
            var span = thread.Rows[0].GetSpan(0);

            if (first is not { } found || span.From.Position < found.Span.From.Position)
            {
                first = (span, thread.ThreadId);
            }
        }

        return first;
    }

    public TracePosition? GetFirstCallStart(string symbol)
    {
        TracePosition? first = null;

        foreach (var (_, span) in GetCalls(symbol))
        {
            if (first is not { } found || span.From.Position < found)
            {
                first = span.From.Position;
            }
        }

        return first;
    }

    public IEnumerable<(uint ThreadId, TraceSpan Span)> GetCalls(string symbol)
    {
        var isMatch = Array.ConvertAll(Nodes, n => n?.Symbol == symbol);

        if (!isMatch.Contains(true))
        {
            yield break;
        }

        foreach (var thread in Threads)
        {
            foreach (var row in thread.Rows)
            {
                for (var index = 0; index < row.Count; index++)
                {
                    var callNodeIndex = row.GetCallNodeIndex(index);

                    if (callNodeIndex >= 0 && callNodeIndex < isMatch.Length && isMatch[callNodeIndex])
                    {
                        yield return (thread.ThreadId, row.GetSpan(index));
                    }
                }
            }
        }
    }

    public TraceTimeline Where(Func<int, bool> include) => new([.. Threads.Select(t => t.Where(include))], this);

    public TraceTimeline? RootedAt(Func<CallTreeNode, bool> isRoot)
    {
        var depths = new int[NodeCount];

        var rootDepths = new int[NodeCount];

        var any = false;

        for (var callNodeIndex = 0; callNodeIndex < depths.Length; callNodeIndex++)
        {
            var parent = Parents[callNodeIndex];

            depths[callNodeIndex] = parent >= 0 ? depths[parent] + 1 : 0;

            rootDepths[callNodeIndex] = parent >= 0 && rootDepths[parent] >= 0
                ? rootDepths[parent]
                : NodeOf(callNodeIndex) is { } callNode && isRoot(callNode) ? depths[callNodeIndex] : -1;

            any |= rootDepths[callNodeIndex] >= 0;
        }

        if (!any)
        {
            return null;
        }

        var rooted = Threads.Select(t => Regroup(t, (_, row, index) => row.GetCallNodeIndex(index) is var n
                                                                       && n >= 0
                                                                       && n < depths.Length
                                                                       && rootDepths[n] >= 0
                                                                           ? depths[n] - rootDepths[n]
                                                                           : -1))
                            .ToList();

        var windows = Windows(rooted.Where(t => t is { CallCount: > 0, Rows.Count: > 0 }).Select(t => t.Rows[0]));

        var threads = new List<TraceTimelineThread>();

        for (var lane = 0; lane < Threads.Count; lane++)
        {
            var thread = rooted[lane].CallCount > 0 ? rooted[lane] : During(Threads[lane], windows, depths);

            if (thread.CallCount > 0)
            {
                threads.Add(thread);
            }
        }

        return new TraceTimeline(threads, this);
    }

    public TraceTimeline? CroppedTo(IReadOnlySet<CallTreeNode> roots)
    {
        var known = new Dictionary<CallTreeNode, int>(ReferenceEqualityComparer.Instance);

        var depths = new int[NodeCount];

        for (var callNodeIndex = 0; callNodeIndex < depths.Length; callNodeIndex++)
        {
            depths[callNodeIndex] = NodeOf(callNodeIndex) is { } callNode ? DepthUnder(roots, callNode, known) : -1;
        }

        List<TraceTimelineThread> threads = [.. Threads.Select(t => Regroup(t, (_, row, index) => GetRowDepth(row, index)))
                                                       .Where(t => t.CallCount > 0)];

        return threads.Count > 0 ? new TraceTimeline(threads, this, fit: true) : null;

        int GetRowDepth(TraceTimelineRow row, int index)
            => row.GetCallNodeIndex(index) is var callNodeIndex && callNodeIndex >= 0 && callNodeIndex < depths.Length ? depths[callNodeIndex] : -1;
    }

    internal void RemoveCallsUnder(Func<CallTreeNode, bool> boundary)
    {
        var removed = new bool[NodeCount];

        var any = false;

        for (var callNodeIndex = 0; callNodeIndex < removed.Length; callNodeIndex++)
        {
            var parent = Parents[callNodeIndex];

            removed[callNodeIndex] = (parent >= 0 && removed[parent]) || (NodeOf(callNodeIndex) is { } callNode && boundary(callNode));

            any |= removed[callNodeIndex];
        }

        if (!any)
        {
            return;
        }

        Func<int, bool> isRemoved = n => n >= 0 && n < removed.Length && removed[n];

        foreach (var thread in Threads)
        {
            thread.Clock.AddDeadSpans(thread.GetMaterialDeadSpans([], isRemoved, Scale));

            thread.RemoveAll(isRemoved);
        }

        SpanCount = Threads.Sum(t => t.CallCount);

        SpanIds = null;

        InstructionCuts = null;

        SetCuts();

        if (Threads.Count > 0)
        {
            End = new TracePoint(Threads.Max(t => t.End.Position), Threads.Max(t => t.End.Instructions));
        }
    }

    internal void SetCuts()
    {
        List<(TracePosition From, TracePosition To)> cuts = [];

        foreach (var thread in Threads)
        {
            foreach (var (from, to) in thread.Clock.GetDeadSpans())
            {
                if (Threads.All(t => ReferenceEquals(t, thread) || !t.HasBoundaryWithin(from, to)))
                {
                    cuts.Add((from, to));
                }
            }
        }

        Scale.SetCuts(cuts);
    }

    public TraceTimeline Folded(Func<CallTreeNode, bool> isFolded)
    {
        var folded = new bool[NodeCount];

        var any = false;

        for (var callNodeIndex = 0; callNodeIndex < folded.Length; callNodeIndex++)
        {
            folded[callNodeIndex] = NodeOf(callNodeIndex) is { } callNode && isFolded(callNode);

            any |= folded[callNodeIndex];
        }

        if (!any)
        {
            return this;
        }

        var lifts = new int[NodeCount];

        int LiftOf(int callNodeIndex, int depth)
        {
            if (depth == 0 || callNodeIndex < 0 || callNodeIndex >= lifts.Length)
            {
                return 0;
            }

            if (lifts[callNodeIndex] < 0)
            {
                var parent = Parents[callNodeIndex];

                lifts[callNodeIndex] = parent < 0 ? 0 : LiftOf(parent, depth - 1) + (folded[parent] ? 1 : 0);
            }

            return lifts[callNodeIndex];
        }

        var threads = new List<TraceTimelineThread>(Threads.Count);

        foreach (var thread in Threads)
        {
            Array.Fill(lifts, -1);

            threads.Add(Regroup(thread, (depth, row, index) => row.GetCallNodeIndex(index) is var n && n >= 0 && n < folded.Length && folded[n]
                                                                   ? -1
                                                                   : depth - LiftOf(n, depth)));
        }

        return new TraceTimeline(threads, this);
    }

    internal void MapNodes(CallTreeNode?[] nodes) => Nodes = nodes;

    internal void SetCalls(TraceCallStore calls) => Calls = calls;

    internal int GetSpanId(int lane, int depth, int index) => GetSpanIds().GetId(lane, depth, index);

    internal int FindSpanId(int callNodeIndex, int callIndex, TracePosition position)
    {
        if (callNodeIndex < 0)
        {
            return -1;
        }

        if (callIndex >= 0
            && NodeOf(callNodeIndex) is { } node
            && FindCall(node, callIndex) is { } location
            && GetSpanId(location.ThreadIndex, location.Depth, location.Index) is var found
            && IsSpanAt(found, callNodeIndex, callIndex, position))
        {
            return found;
        }

        for (var lane = 0; lane < Threads.Count; lane++)
        {
            var rows = Threads[lane].Rows;

            for (var depth = rows.Count - 1; depth >= 0; depth--)
            {
                if (rows[depth].GetIndex(position) is >= 0 and var index
                    && rows[depth].GetCallNodeIndex(index) == callNodeIndex
                    && rows[depth].GetCallIndex(index) == callIndex)
                {
                    return GetSpanId(lane, depth, index);
                }
            }
        }

        return -1;
    }

    internal bool IsSpanAt(int spanId, int callNodeIndex, int callIndex, TracePosition position)
        => GetSpanIds().TryGet(spanId, out var row, out var index)
           && row.GetCallNodeIndex(index) == callNodeIndex
           && row.GetCallIndex(index) == callIndex
           && row.GetFromPosition(index) <= position
           && position < row.GetToPosition(index);

    internal (int CallNodeIndex, int CallIndex) GetSpanCall(int spanId)
        => GetSpanIds().TryGet(spanId, out var row, out var index) ? (row.GetCallNodeIndex(index), row.GetCallIndex(index)) : (-1, -1);

    internal ulong GetInstructions(int spanId, TracePosition position)
        => GetSpanIds().TryGet(spanId, out var row, out var index) ? GetInstructions(row.GetSpan(index), position) : 0;

    internal ulong GetInstructions(TraceSpan span, TracePosition position)
    {
        var from = Scale.GetValue(span.From.Position);

        var to = Scale.GetValue(span.To.Position);

        var fraction = to > from ? Math.Clamp((Scale.GetValue(position) - from) / (to - from), 0, 1) : 0;

        return span.From.Instructions + (ulong)((span.To.Instructions - span.From.Instructions) * fraction);
    }

    internal Dictionary<CallTreeNode, int[]> GetCallActivity(int buckets)
    {
        var activity = new Dictionary<CallTreeNode, int[]>(ReferenceEqualityComparer.Instance);

        var start = GetStart(TraceTimelineAxis.Position);

        var length = GetEnd(TraceTimelineAxis.Position) - start;

        if (buckets <= 0 || length <= 0)
        {
            return activity;
        }

        foreach (var span in GetSpanLocations())
        {
            if ((span.Row.FlagsAt(span.Index) & TraceSpanFlags.StartUnknown) != 0)
            {
                continue;
            }

            ref var counts = ref CollectionsMarshal.GetValueRefOrAddDefault(activity, span.Node, out _);

            counts ??= new int[buckets];

            var bucket = (int)((span.Row.GetStart(TraceTimelineAxis.Position, span.Index) - start) / length * buckets);

            counts[Math.Clamp(bucket, 0, buckets - 1)]++;
        }

        return activity;
    }

    private TraceSpanIdIndex GetSpanIds() => SpanIds ??= TraceSpanIdIndex.Build(Threads);

    private int DepthOf(int callNodeIndex)
    {
        var depth = 0;

        for (var current = callNodeIndex >= 0 && callNodeIndex < Parents.Length ? Parents[callNodeIndex] : -1; current >= 0; current = Parents[current])
        {
            depth++;
        }

        return depth;
    }

    private static List<(TracePosition Start, TracePosition End)> Windows(IEnumerable<TraceTimelineRow> roots)
    {
        var windows = new List<(TracePosition Start, TracePosition End)>();

        foreach (var row in roots)
        {
            for (var index = 0; index < row.Count; index++)
            {
                windows.Add((row.GetFrom(index).Position, row.GetTo(index).Position));
            }
        }

        windows.Sort();

        var merged = new List<(TracePosition Start, TracePosition End)>();

        foreach (var window in windows)
        {
            if (merged.Count > 0 && window.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, window.End > merged[^1].End ? window.End : merged[^1].End);
            }
            else
            {
                merged.Add(window);
            }
        }

        return merged;
    }

    private static int DepthUnder(IReadOnlySet<CallTreeNode> roots, CallTreeNode node, Dictionary<CallTreeNode, int> known)
    {
        if (known.TryGetValue(node, out var depth))
        {
            return depth;
        }

        if (node.Parent is not { } parent || !parent.Children.TryGetValue(node.Key, out var child) || !ReferenceEquals(child, node))
        {
            depth = -1;
        }
        else if (roots.Contains(node))
        {
            depth = 0;
        }
        else
        {
            var parentDepth = DepthUnder(roots, parent, known);

            depth = parentDepth < 0 ? -1 : parentDepth + 1;
        }

        known[node] = depth;

        return depth;
    }

    private TraceTimelineThread During(TraceTimelineThread thread, List<(TracePosition Start, TracePosition End)> windows, int[] depths)
    {
        TracePosition[] windowEnds = [.. windows.Select(w => w.End)];

        var shallowest = int.MaxValue;

        foreach (var row in thread.Rows)
        {
            for (var index = 0; index < row.Count; index++)
            {
                if (Overlaps(row, index))
                {
                    shallowest = Math.Min(shallowest, GetRowDepth(row, index));
                }
            }
        }

        return shallowest == int.MaxValue
            ? new TraceTimelineThread(thread.ThreadId, thread.Clock, thread.Store, [], 0)
            : Regroup(thread, (_, row, index) => Overlaps(row, index) ? GetRowDepth(row, index) - shallowest : -1);

        bool Overlaps(TraceTimelineRow row, int index)
        {
            var first = SortedSearch.FirstAtOrAfter(windowEnds, row.GetFrom(index).Position);

            return first < windows.Count && windows[first].Start <= row.GetTo(index).Position;
        }

        int GetRowDepth(TraceTimelineRow row, int index)
            => row.GetCallNodeIndex(index) is var callNodeIndex && callNodeIndex >= 0 && callNodeIndex < depths.Length ? depths[callNodeIndex] : 0;
    }

    private TraceTimelineThread Regroup(TraceTimelineThread thread, Func<int, TraceTimelineRow, int, int> depthOf)
    {
        var sources = new List<RowSource>();

        for (var source = 0; source < thread.Rows.Count; source++)
        {
            var row = thread.Rows[source];

            for (var index = 0; index < row.Count; index++)
            {
                var depth = depthOf(source, row, index);

                if (depth < 0)
                {
                    continue;
                }

                while (sources.Count <= depth)
                {
                    sources.Add(new RowSource());
                }

                sources[depth].Add(source, index);
            }
        }

        var indexes = sources.Select(s => s is { Count: > 0, IsSlice: true } ? null : new int[s.Count]).ToArray();

        if (sources.Any(s => s is { Count: > 0, IsSlice: false }))
        {
            Merge(thread, depthOf, indexes);
        }

        TraceTimelineRow[] rows = [.. sources.Select((s, depth) => indexes[depth] is { } rowIndexes
                                                                   ? new TraceTimelineRow(Scale, thread.Clock, thread.Store, rowIndexes)
                                                                   : thread.Rows[s.Row].Slice(s.First, s.Count))];

        return new TraceTimelineThread(thread.ThreadId, thread.Clock, thread.Store, rows, sources.Sum(s => s.Count));
    }

    private static void Merge(TraceTimelineThread thread, Func<int, TraceTimelineRow, int, int> depthOf, int[]?[] indexes)
    {
        var rows = thread.Rows;

        var filled = new int[indexes.Length];

        var cursors = new PriorityQueue<(int Source, int Index), TracePosition>(rows.Count);

        for (var source = 0; source < rows.Count; source++)
        {
            if (rows[source].Count > 0)
            {
                cursors.Enqueue((source, 0), rows[source].GetFromPosition(0));
            }
        }

        while (cursors.TryDequeue(out var cursor, out _))
        {
            var (source, index) = cursor;

            var row = rows[source];

            if (index + 1 < row.Count)
            {
                cursors.Enqueue((source, index + 1), row.GetFromPosition(index + 1));
            }

            var depth = depthOf(source, row, index);

            if (depth < 0 || indexes[depth] is not { } target)
            {
                continue;
            }

            target[filled[depth]++] = row.GetStoreIndex(index);
        }
    }

    internal sealed class Builder
    {
        private Dictionary<uint, ThreadSpanList> Threads { get; } = [];

        public unsafe void Add(IntPtr spans, int count) => Add(new ReadOnlySpan<CallSpan>((void*)spans, count));

        public void Add(ReadOnlySpan<CallSpan> spans)
        {
            foreach (var span in spans)
            {
                if (!Threads.TryGetValue(span.Thread, out var thread))
                {
                    thread = new ThreadSpanList();

                    Threads[span.Thread] = thread;
                }

                thread.Add(span);
            }
        }

        public TraceTimeline Build(CallNode[] nodes)
        {
            var depths = new int[nodes.Length];

            var parents = new int[nodes.Length];

            for (var index = 0; index < nodes.Length; index++)
            {
                var parent = nodes[index].ParentIndex;

                parents[index] = parent >= 0 && parent < index ? parent : -1;

                depths[index] = parents[index] >= 0 ? depths[parent] + 1 : 0;
            }

            var scale = new TraceScale(Threads.Values.Select(t => t.MaximumSteps).DefaultIfEmpty(0ul).Max() + 1d);

            var threads = Threads.Select(t => t.Value.Build(t.Key, depths, scale))
                                 .Where(t => t.CallCount > 0)
                                 .OrderBy(t => t.Start.Position)
                                 .ToList();

            var timeline = new TraceTimeline(threads, parents, scale);

            timeline.SetCuts();

            return timeline;
        }
    }

    private sealed class ThreadSpanList
    {
        private const int ExcludedNode = -2;

        private const int ChunkShift = 11;

        private const int ChunkSize = 1 << ChunkShift;

        private const int ChunkMask = ChunkSize - 1;

        public ulong MaximumSteps { get; private set; }

        private List<PendingSpan[]?> Chunks { get; } = [];

        private TraceThreadClock.Builder Clock { get; } = new();

        private int Count { get; set; }

        private List<(TracePosition From, TracePosition To)> Excluded { get; } = [];

        public void Add(CallSpan span)
        {
            var from = new TracePosition(span.StartSequence, span.StartSteps);

            var to = new TracePosition(span.EndSequence, span.EndSteps);

            Clock.Add(from, span.StartInstructions);

            Clock.Add(to, span.EndInstructions);

            MaximumSteps = Math.Max(MaximumSteps, Math.Max(span.StartSteps, span.EndSteps));

            if (span.CallNodeIndex == ExcludedNode)
            {
                Excluded.Add((from, to));

                return;
            }

            if ((Count & ChunkMask) == 0)
            {
                Chunks.Add(new PendingSpan[ChunkSize]);
            }

            Chunks[^1]![Count & ChunkMask] = new PendingSpan(from, to, span.CallNodeIndex, span.CallIndex, (byte)span.Flags);

            Count++;
        }

        public TraceTimelineThread Build(uint threadId, int[] depths, TraceScale scale)
        {
            var count = Count;

            var rowCounts = new List<int>();

            for (var index = 0; index < count; index++)
            {
                var depth = GetDepth(Chunks[index >> ChunkShift]![index & ChunkMask], depths);

                while (rowCounts.Count <= depth)
                {
                    rowCounts.Add(0);
                }

                rowCounts[depth]++;
            }

            var starts = new int[rowCounts.Count];

            for (var depth = 1; depth < starts.Length; depth++)
            {
                starts[depth] = starts[depth - 1] + rowCounts[depth - 1];
            }

            var next = (int[])starts.Clone();

            var store = new TraceSpanStore(count);

            for (var chunk = 0; chunk < Chunks.Count; chunk++)
            {
                var spans = Chunks[chunk]!;

                var filled = Math.Min(ChunkSize, count - (chunk << ChunkShift));

                for (var offset = 0; offset < filled; offset++)
                {
                    var span = spans[offset];

                    store.Set(next[GetDepth(span, depths)]++, span.From, span.To, span.CallNodeIndex, span.CallIndex, span.Flags);
                }

                Chunks[chunk] = null;
            }

            Chunks.Clear();

            Count = 0;

            var clock = Clock.Build();

            var rows = new TraceTimelineRow[starts.Length];

            for (var depth = 0; depth < rows.Length; depth++)
            {
                store.Sort(starts[depth], rowCounts[depth]);

                rows[depth] = new TraceTimelineRow(scale, clock, store, starts[depth], rowCounts[depth]);
            }

            var thread = new TraceTimelineThread(threadId, clock, store, rows, count);

            clock.AddDeadSpans(thread.GetMaterialDeadSpans(Excluded, _ => false, scale));

            Excluded.Clear();

            return thread;
        }

        private static int GetDepth(in PendingSpan span, int[] depths)
            => span.CallNodeIndex >= 0 && span.CallNodeIndex < depths.Length ? depths[span.CallNodeIndex] : 0;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private readonly record struct PendingSpan(TracePosition From, TracePosition To, int CallNodeIndex, uint CallIndex, byte Flags);
    }

    private sealed class RowSource
    {
        public int Count { get; private set; }

        public int Row { get; private set; } = -1;

        public int First { get; private set; }

        public bool IsSlice { get; private set; } = true;

        public void Add(int row, int index)
        {
            if (Count == 0)
            {
                Row = row;

                First = index;
            }
            else if (row != Row || index != First + Count)
            {
                IsSlice = false;
            }

            Count++;
        }
    }
}
