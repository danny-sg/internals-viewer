using InternalsViewer.Query.CallStack;
using System.Buffers;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimeline
{
    internal TraceTimeline(IReadOnlyList<TraceTimelineThread> threads, int[] parents, double stepsPerSequence)
    {
        Threads = threads;
        Parents = parents;
        StepsPerSequence = stepsPerSequence;
        SpanCount = threads.Sum(t => t.Calls);

        if (threads.Count == 0)
        {
            return;
        }

        PositionStart = threads.Min(t => t.StartOf(TraceTimelineAxis.Position));
        PositionEnd = threads.Max(t => t.EndOf(TraceTimelineAxis.Position));
        InstructionEnd = threads.Max(t => t.EndOf(TraceTimelineAxis.Instructions));
    }

    private TraceTimeline(IReadOnlyList<TraceTimelineThread> threads, TraceTimeline source, bool fit = false)
    {
        Threads = threads;
        Parents = source.Parents;
        Nodes = source.Nodes;
        StepsPerSequence = source.StepsPerSequence;
        SpanCount = threads.Sum(t => t.Calls);
        PositionStart = source.PositionStart;
        PositionEnd = source.PositionEnd;
        InstructionStart = source.InstructionStart;
        InstructionEnd = source.InstructionEnd;
        Memory = source.Memory;
        Calls = source.Calls;
        Lifetimes = source.Lifetimes;
        EventLinks = source.EventLinks;
        EventLinksBySequence = source.EventLinksBySequence;

        if (!fit || threads.Count == 0)
        {
            return;
        }

        PositionStart = threads.Min(t => t.StartOf(TraceTimelineAxis.Position));
        PositionEnd = threads.Max(t => t.EndOf(TraceTimelineAxis.Position));
        InstructionStart = threads.Min(t => t.StartOf(TraceTimelineAxis.Instructions));
        InstructionEnd = threads.Max(t => t.EndOf(TraceTimelineAxis.Instructions));
    }

    public IReadOnlyList<TraceTimelineThread> Threads { get; }

    public int NodeCount => Parents.Length;

    public long SpanCount { get; }

    public MemoryIndex Memory { get; private set; } = MemoryIndex.Empty;

    public IReadOnlyList<TraceOperatorLifetime> Lifetimes { get; private set; } = [];

    public IReadOnlyList<TraceEventLink> EventLinks { get; private set; } = [];

    internal CallStackNode?[] MappedNodes => Nodes;

    private double PositionStart { get; }

    private double PositionEnd { get; }

    private double InstructionStart { get; }

    private double InstructionEnd { get; }

    private double StepsPerSequence { get; }

    private int[] Parents { get; }

    private CallStackNode?[] Nodes { get; set; } = [];

    private Dictionary<int, TraceEventLink> EventLinksBySequence { get; set; } = [];

    private TraceCallStore? Calls { get; set; }

    public double StartOf(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? PositionStart : InstructionStart;

    public double EndOf(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? PositionEnd : InstructionEnd;

    public double StepOf(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? 1 / StepsPerSequence : 1;

    public CallStackNode? NodeOf(int node) => node >= 0 && node < Nodes.Length ? Nodes[node] : null;

    public TraceSpanLocation? FindCall(CallStackNode node, int call)
        => node.Frame is { } frame ? FindCall(frame.Address, frame.Instance, call) : null;

    public TraceSpanLocation? FindCall(ulong address, ulong instance, int call)
    {
        if (Calls?.GetCallList(address, instance) is not { } calls || call < 0 || call >= calls.Count)
        {
            return null;
        }

        var node = calls.GetNodeIndex(call);

        if (NodeOf(node) is not { } callNode)
        {
            return null;
        }

        var thread = calls.GetThread(call);

        var start = PositionOf(calls.GetFromPosition(call));

        var depth = DepthOf(node);

        for (var lane = 0; lane < Threads.Count; lane++)
        {
            if (Threads[lane].ThreadId != thread)
            {
                continue;
            }

            var rows = Threads[lane].Rows;

            if (depth < rows.Count && IndexOf(rows[depth], start, node, call) is >= 0 and var expected)
            {
                return new TraceSpanLocation(lane, Threads[lane], depth, rows[depth], expected, callNode);
            }

            for (var row = 0; row < rows.Count; row++)
            {
                if (row != depth && IndexOf(rows[row], start, node, call) is >= 0 and var index)
                {
                    return new TraceSpanLocation(lane, Threads[lane], row, rows[row], index, callNode);
                }
            }
        }

        return null;
    }

    public IReadOnlyList<TraceThreadStack> StackAt(TraceTimelineAxis axis, double position)
    {
        var stacks = new List<TraceThreadStack>();

        foreach (var thread in Threads)
        {
            var calls = new List<TraceStackCall>();

            foreach (var row in thread.Rows)
            {
                var index = row.IndexAt(axis, position, 0);

                if (index < 0)
                {
                    continue;
                }

                var instructions = row.Span(TraceTimelineAxis.Instructions, index);

                calls.Add(new TraceStackCall(row.Span(axis, index), instructions.End - instructions.Start));
            }

            if (calls.Count > 0)
            {
                stacks.Add(new TraceThreadStack(thread.ThreadId, calls));
            }
        }

        return stacks;
    }

    public TraceEventLink? EventLinkOf(EngineEvent engineEvent)
    {
        foreach (var owned in engineEvent.SelfAndOwned())
        {
            if (EventLinksBySequence.TryGetValue(owned.SequenceId, out var link))
            {
                return link;
            }
        }

        return null;
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
                    if (NodeOf(row.NodeAt(index)) is { } node)
                    {
                        yield return new TraceSpanLocation(threadIndex, thread, depth, row, index, node);
                    }
                }
            }
        }
    }

    public IReadOnlyList<TraceSelfAllocation> SelfAllocations()
    {
        var totals = new Dictionary<(int Thread, int Depth, int Index), ulong>();

        for (var thread = 0; thread < Threads.Count; thread++)
        {
            var rows = Threads[thread].Rows;

            foreach (var (position, bytes) in Memory.GetAllocations(Threads[thread].ThreadId))
            {
                for (var depth = rows.Count - 1; depth >= 0; depth--)
                {
                    var index = rows[depth].IndexAt(TraceTimelineAxis.Position, position, 0);

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

    public int ParentOf(int node) => node >= 0 && node < Parents.Length ? Parents[node] : -1;

    public TraceTimeline Where(Func<int, bool> include) => new([.. Threads.Select(t => t.Where(include))], this);

    public TraceTimeline? RootedAt(Func<CallStackNode, bool> isRoot)
    {
        var depths = new int[NodeCount];

        var rootDepths = new int[NodeCount];

        var any = false;

        for (var node = 0; node < depths.Length; node++)
        {
            var parent = Parents[node];

            depths[node] = parent >= 0 ? depths[parent] + 1 : 0;

            rootDepths[node] = parent >= 0 && rootDepths[parent] >= 0
                ? rootDepths[parent]
                : NodeOf(node) is { } callNode && isRoot(callNode) ? depths[node] : -1;

            any |= rootDepths[node] >= 0;
        }

        if (!any)
        {
            return null;
        }

        var rooted = Threads.Select(t => Regroup(t, (_, row, index) => row.NodeAt(index) is var n
                                                                       && n >= 0
                                                                       && n < depths.Length
                                                                       && rootDepths[n] >= 0
                                                                           ? depths[n] - rootDepths[n]
                                                                           : -1))
                            .ToList();

        var windows = Windows(rooted.Where(t => t.Calls > 0 && t.Rows.Count > 0).Select(t => t.Rows[0]));

        var threads = new List<TraceTimelineThread>();

        for (var lane = 0; lane < Threads.Count; lane++)
        {
            var thread = rooted[lane].Calls > 0 ? rooted[lane] : During(Threads[lane], windows, depths);

            if (thread.Calls > 0)
            {
                threads.Add(thread);
            }
        }

        return new TraceTimeline(threads, this);
    }

    public TraceTimeline? CroppedTo(IReadOnlySet<CallStackNode> roots)
    {
        var known = new Dictionary<CallStackNode, int>(ReferenceEqualityComparer.Instance);

        var depths = new int[NodeCount];

        for (var node = 0; node < depths.Length; node++)
        {
            depths[node] = NodeOf(node) is { } callNode ? DepthUnder(roots, callNode, known) : -1;
        }

        List<TraceTimelineThread> threads = [.. Threads.Select(t => Regroup(t, (_, row, index) => DepthOf(row, index)))
                                                       .Where(t => t.Calls > 0)];

        return threads.Count > 0 ? new TraceTimeline(threads, this, fit: true) : null;

        int DepthOf(TraceTimelineRow row, int index)
            => row.NodeAt(index) is var node && node >= 0 && node < depths.Length ? depths[node] : -1;
    }

    public TraceTimeline WithoutCallsUnder(Func<CallStackNode, bool> boundary)
    {
        var removed = new bool[NodeCount];

        var any = false;

        for (var node = 0; node < removed.Length; node++)
        {
            var parent = Parents[node];

            removed[node] = (parent >= 0 && removed[parent]) || (NodeOf(node) is { } callNode && boundary(callNode));

            any |= removed[node];
        }

        return any ? Where(n => n < 0 || n >= removed.Length || !removed[n]) : this;
    }

    public TraceTimeline Folded(Func<CallStackNode, bool> isFolded)
    {
        var folded = new bool[NodeCount];

        var any = false;

        for (var node = 0; node < folded.Length; node++)
        {
            folded[node] = NodeOf(node) is { } callNode && isFolded(callNode);

            any |= folded[node];
        }

        if (!any)
        {
            return this;
        }

        var lifts = new int[NodeCount];

        int LiftOf(int node, int depth)
        {
            if (depth == 0 || node < 0 || node >= lifts.Length)
            {
                return 0;
            }

            if (lifts[node] < 0)
            {
                var parent = Parents[node];

                lifts[node] = parent < 0 ? 0 : LiftOf(parent, depth - 1) + (folded[parent] ? 1 : 0);
            }

            return lifts[node];
        }

        var threads = new List<TraceTimelineThread>(Threads.Count);

        foreach (var thread in Threads)
        {
            Array.Fill(lifts, -1);

            threads.Add(Regroup(thread, (depth, row, index) => row.NodeAt(index) is var n && n >= 0 && n < folded.Length && folded[n]
                                                                   ? -1
                                                                   : depth - LiftOf(n, depth)));
        }

        return new TraceTimeline(threads, this);
    }

    public static double PositionOf(ulong sequence, ulong steps, double stepsPerSequence) => sequence + steps / stepsPerSequence;

    internal void SetMemory(IEnumerable<MemoryAllocation> allocations, IEnumerable<MemoryFree> frees)
        => Memory = MemoryIndex.Build(allocations, frees);

    internal void MapNodes(CallStackNode?[] nodes) => Nodes = nodes;

    internal void SetLifetimes(IReadOnlyList<TraceOperatorLifetime> lifetimes) => Lifetimes = lifetimes;

    internal void SetCalls(TraceCallStore calls) => Calls = calls;

    internal Dictionary<ulong, ExecutionOperatorEvent> GetOperatorsByInstance()
    {
        var owners = new Dictionary<ulong, ExecutionOperatorEvent>();

        foreach (var lifetime in Lifetimes)
        {
            foreach (var instance in lifetime.Operator.Instances)
            {
                owners.TryAdd(instance, lifetime.Operator);
            }
        }

        return owners;
    }

    private static int IndexOf(TraceTimelineRow row, double start, int node, int call)
    {
        var starts = row.Starts(TraceTimelineAxis.Position);

        var index = SortedSearch.FirstAtOrAfter(starts, start);

        return index < row.Count && starts[index] == start && row.NodeAt(index) == node && row.CallAt(index) == call ? index : -1;
    }

    private int DepthOf(int node)
    {
        var depth = 0;

        for (var current = node >= 0 && node < Parents.Length ? Parents[node] : -1; current >= 0; current = Parents[current])
        {
            depth++;
        }

        return depth;
    }

    internal void SetEventLinks(IReadOnlyList<TraceEventLink> links)
    {
        EventLinks = links;

        EventLinksBySequence = [];

        foreach (var link in links)
        {
            EventLinksBySequence.TryAdd(link.Event.SequenceId, link);
        }
    }

    public double PositionOf(TracePosition position)
        => PositionOf(position.Sequence, Math.Min(position.Steps, (ulong)Math.Max(StepsPerSequence - 1, 0)), StepsPerSequence);

    internal Dictionary<CallStackNode, int[]> GetCallActivity(int buckets)
    {
        var activity = new Dictionary<CallStackNode, int[]>(ReferenceEqualityComparer.Instance);

        var start = StartOf(TraceTimelineAxis.Position);

        var length = EndOf(TraceTimelineAxis.Position) - start;

        if (buckets <= 0 || length <= 0)
        {
            return activity;
        }

        foreach (var span in GetSpanLocations())
        {
            if (span.Row.FlagsAt(span.Index).HasFlag(TraceSpanFlags.StartUnknown))
            {
                continue;
            }

            ref var counts = ref CollectionsMarshal.GetValueRefOrAddDefault(activity, span.Node, out _);

            counts ??= new int[buckets];

            var bucket = (int)((span.StartOf(TraceTimelineAxis.Position) - start) / length * buckets);

            counts[Math.Clamp(bucket, 0, buckets - 1)]++;
        }

        return activity;
    }

    private static List<(double Start, double End)> Windows(IEnumerable<TraceTimelineRow> roots)
    {
        var windows = new List<(double Start, double End)>();

        foreach (var row in roots)
        {
            var starts = row.Starts(TraceTimelineAxis.Position);

            var ends = row.Ends(TraceTimelineAxis.Position);

            for (var index = 0; index < row.Count; index++)
            {
                windows.Add((starts[index], ends[index]));
            }
        }

        windows.Sort();

        var merged = new List<(double Start, double End)>();

        foreach (var window in windows)
        {
            if (merged.Count > 0 && window.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, window.End));
            }
            else
            {
                merged.Add(window);
            }
        }

        return merged;
    }

    private static int DepthUnder(IReadOnlySet<CallStackNode> roots, CallStackNode node, Dictionary<CallStackNode, int> known)
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

    private static TraceTimelineThread During(TraceTimelineThread thread,
                                              List<(double Start, double End)> windows,
                                              int[] depths)
    {
        double[] windowEnds = [.. windows.Select(w => w.End)];

        bool Overlaps(TraceTimelineRow row, int index)
        {
            var first = SortedSearch.FirstAtOrAfter(windowEnds, row.Starts(TraceTimelineAxis.Position)[index]);

            return first < windows.Count && windows[first].Start <= row.Ends(TraceTimelineAxis.Position)[index];
        }

        int DepthOf(TraceTimelineRow row, int index)
            => row.NodeAt(index) is var node && node >= 0 && node < depths.Length ? depths[node] : 0;

        var shallowest = int.MaxValue;

        foreach (var row in thread.Rows)
        {
            for (var index = 0; index < row.Count; index++)
            {
                if (Overlaps(row, index))
                {
                    shallowest = Math.Min(shallowest, DepthOf(row, index));
                }
            }
        }

        return shallowest == int.MaxValue
            ? new TraceTimelineThread(thread.ThreadId, [], 0)
            : Regroup(thread, (_, row, index) => Overlaps(row, index) ? DepthOf(row, index) - shallowest : -1);
    }

    private static TraceTimelineThread Regroup(TraceTimelineThread thread,
                                               Func<int, TraceTimelineRow, int, int> depthOf)
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

        var copies = sources.Select(s => s is { Count: > 0, IsSlice: true } ? null : new RowArrays(s.Count)).ToArray();

        if (sources.Any(s => s is { Count: > 0, IsSlice: false }))
        {
            Copy(thread, depthOf, copies);
        }

        TraceTimelineRow[] rows = [.. sources.Select((s, depth) => copies[depth]?.Build() ?? thread.Rows[s.Row].Slice(s.First, s.Count))];

        return new TraceTimelineThread(thread.ThreadId, rows, sources.Sum(s => s.Count));
    }

    private static void Copy(TraceTimelineThread thread, Func<int, TraceTimelineRow, int, int> depthOf, RowArrays?[] copies)
    {
        for (var source = 0; source < thread.Rows.Count; source++)
        {
            var row = thread.Rows[source];

            var positionStarts = row.Starts(TraceTimelineAxis.Position);

            var positionEnds = row.Ends(TraceTimelineAxis.Position);

            var instructionStarts = row.Starts(TraceTimelineAxis.Instructions);

            var instructionEnds = row.Ends(TraceTimelineAxis.Instructions);

            for (var index = 0; index < row.Count; index++)
            {
                var depth = depthOf(source, row, index);

                if (depth < 0 || copies[depth] is not { } copy)
                {
                    continue;
                }

                var call = row.CallAt(index);

                copy.Add(positionStarts[index],
                         positionEnds[index],
                         instructionStarts[index],
                         instructionEnds[index],
                         row.NodeAt(index),
                         call < 0 ? uint.MaxValue : (uint)call,
                         (byte)row.FlagsAt(index));
            }
        }
    }

    internal sealed class Builder
    {
        private Dictionary<uint, ThreadSpans> Threads { get; } = [];

        public void Add(IntPtr spans, int count)
        {
            var size = count * Marshal.SizeOf<CallSpan>();

            var buffer = ArrayPool<byte>.Shared.Rent(size);

            try
            {
                Marshal.Copy(spans, buffer, 0, size);

                Add(MemoryMarshal.Cast<byte, CallSpan>(buffer.AsSpan(0, size)));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void Add(ReadOnlySpan<CallSpan> spans)
        {
            foreach (var span in spans)
            {
                if (!Threads.TryGetValue(span.Thread, out var thread))
                {
                    thread = new ThreadSpans();

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
                var parent = nodes[index].Parent;

                parents[index] = parent >= 0 && parent < index ? parent : -1;

                depths[index] = parents[index] >= 0 ? depths[parent] + 1 : 0;
            }

            var stepsPerSequence = Threads.Values.Select(t => t.MaximumSteps).DefaultIfEmpty(0ul).Max() + 1d;

            var threads = Threads.Select(t => t.Value.Build(t.Key, depths, stepsPerSequence))
                                 .Where(t => t.Calls > 0)
                                 .OrderBy(t => t.StartOf(TraceTimelineAxis.Position))
                                 .ToList();

            return new TraceTimeline(threads, parents, stepsPerSequence);
        }
    }

    private sealed class ThreadSpans
    {
        private const int ChunkShift = 12;

        private const int ChunkSize = 1 << ChunkShift;

        private const int ChunkMask = ChunkSize - 1;

        public ulong MaximumSteps { get; private set; }

        private List<CallSpan[]?> Chunks { get; } = [];

        private int Count { get; set; }

        public void Add(CallSpan span)
        {
            if ((Count & ChunkMask) == 0)
            {
                Chunks.Add(new CallSpan[ChunkSize]);
            }

            Chunks[^1]![Count & ChunkMask] = span;

            Count++;

            MaximumSteps = Math.Max(MaximumSteps, Math.Max(span.StartSteps, span.EndSteps));
        }

        public TraceTimelineThread Build(uint threadId, int[] depths, double stepsPerSequence)
        {
            var count = Count;

            var rowCounts = new List<int>();

            var rowOf = new int[count];

            for (var index = 0; index < count; index++)
            {
                var node = Chunks[index >> ChunkShift]![index & ChunkMask].Node;

                var depth = node >= 0 && node < depths.Length ? depths[node] : 0;

                while (rowCounts.Count <= depth)
                {
                    rowCounts.Add(0);
                }

                rowOf[index] = depth;

                rowCounts[depth]++;
            }

            var rows = rowCounts.Select(c => new RowArrays(c)).ToArray();

            for (var chunk = 0; chunk < Chunks.Count; chunk++)
            {
                var spans = Chunks[chunk]!;

                var first = chunk << ChunkShift;

                var filled = Math.Min(ChunkSize, count - first);

                for (var offset = 0; offset < filled; offset++)
                {
                    var span = spans[offset];

                    rows[rowOf[first + offset]].Add(PositionOf(span.StartSequence, span.StartSteps, stepsPerSequence),
                                                    PositionOf(span.EndSequence, span.EndSteps, stepsPerSequence),
                                                    span.StartInstructions,
                                                    span.EndInstructions,
                                                    span.Node,
                                                    span.Call,
                                                    (byte)span.Flags);
                }

                Chunks[chunk] = null;
            }

            Chunks.Clear();

            Count = 0;

            return new TraceTimelineThread(threadId, [.. rows.Select(r => r.Build())], count);
        }
    }

    private sealed class RowArrays(int count)
    {
        private double[] PositionStarts { get; } = new double[count];

        private double[] PositionEnds { get; } = new double[count];

        private double[] InstructionStarts { get; } = new double[count];

        private double[] InstructionEnds { get; } = new double[count];

        private int[] Nodes { get; } = new int[count];

        private uint[] Calls { get; } = new uint[count];

        private byte[] Flags { get; } = new byte[count];

        private int Filled { get; set; }

        public void Add(double positionStart,
                        double positionEnd,
                        double instructionStart,
                        double instructionEnd,
                        int node,
                        uint call,
                        byte flags)
        {
            PositionStarts[Filled] = positionStart;
            PositionEnds[Filled] = positionEnd;
            InstructionStarts[Filled] = instructionStart;
            InstructionEnds[Filled] = instructionEnd;
            Nodes[Filled] = node;
            Calls[Filled] = call;
            Flags[Filled] = flags;

            Filled++;
        }

        public TraceTimelineRow Build()
        {
            if (!IsSorted())
            {
                var order = Enumerable.Range(0, Filled).ToArray();

                Array.Sort([.. PositionStarts], order);

                Reorder(PositionStarts, order);
                Reorder(PositionEnds, order);
                Reorder(InstructionStarts, order);
                Reorder(InstructionEnds, order);
                Reorder(Nodes, order);
                Reorder(Calls, order);
                Reorder(Flags, order);
            }

            return new TraceTimelineRow(PositionStarts,
                                        PositionEnds,
                                        InstructionStarts,
                                        InstructionEnds,
                                        Nodes,
                                        Calls,
                                        Flags);
        }

        private bool IsSorted()
        {
            for (var index = 1; index < Filled; index++)
            {
                if (PositionStarts[index] < PositionStarts[index - 1])
                {
                    return false;
                }
            }

            return true;
        }

        private static void Reorder<T>(T[] values, int[] order)
        {
            var copy = (T[])values.Clone();

            for (var index = 0; index < order.Length; index++)
            {
                values[index] = copy[order[index]];
            }
        }
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
