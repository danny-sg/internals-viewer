using System.Buffers;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using InternalsViewer.Query.CallStack.TimeTravel.Native;

namespace InternalsViewer.Query.CallStack.TimeTravel.Timeline;

public sealed class TimeTravelTimeline
{
    internal TimeTravelTimeline(IReadOnlyList<TimeTravelTimelineThread> threads, int[] parents, double stepsPerSequence)
    {
        Threads = threads;
        Parents = parents;
        StepsPerSequence = stepsPerSequence;
        SpanCount = threads.Sum(t => t.Calls);

        if (threads.Count == 0)
        {
            return;
        }

        PositionStart = threads.Min(t => t.StartOf(TimeTravelTimelineAxis.Position));
        PositionEnd = threads.Max(t => t.EndOf(TimeTravelTimelineAxis.Position));
        InstructionEnd = threads.Max(t => t.EndOf(TimeTravelTimelineAxis.Instructions));
    }

    private TimeTravelTimeline(IReadOnlyList<TimeTravelTimelineThread> threads, TimeTravelTimeline source)
    {
        Threads = threads;
        Parents = source.Parents;
        Nodes = source.Nodes;
        StepsPerSequence = source.StepsPerSequence;
        SpanCount = threads.Sum(t => t.Calls);
        PositionStart = source.PositionStart;
        PositionEnd = source.PositionEnd;
        InstructionEnd = source.InstructionEnd;
        Memory = source.Memory;
    }

    public IReadOnlyList<TimeTravelTimelineThread> Threads { get; }

    public int NodeCount => Parents.Length;

    public long SpanCount { get; }

    public bool HasAllocations => Memory.HasAllocations;

    private double PositionStart { get; }

    private double PositionEnd { get; }

    private double InstructionEnd { get; }

    private double StepsPerSequence { get; }

    private int[] Parents { get; }

    private CallStackNode?[] Nodes { get; set; } = [];

    private TimeTravelMemoryIndex Memory { get; set; } = TimeTravelMemoryIndex.Empty;

    public double StartOf(TimeTravelTimelineAxis axis) => axis == TimeTravelTimelineAxis.Position ? PositionStart : 0;

    public double EndOf(TimeTravelTimelineAxis axis) => axis == TimeTravelTimelineAxis.Position ? PositionEnd : InstructionEnd;

    public double StepOf(TimeTravelTimelineAxis axis) => axis == TimeTravelTimelineAxis.Position ? 1 / StepsPerSequence : 1;

    public CallStackNode? NodeOf(int node) => node >= 0 && node < Nodes.Length ? Nodes[node] : null;

    public (ulong Bytes, int Count) AllocatedDuring(uint thread, double positionStart, double positionEnd)
        => Memory.AllocatedDuring(thread, positionStart, positionEnd);

    public ulong FreedDuring(uint thread, double positionStart, double positionEnd)
        => Memory.FreedDuring(thread, positionStart, positionEnd);

    public ulong RetainedBy(uint thread, double positionStart, double positionEnd)
        => Memory.RetainedBy(thread, positionStart, positionEnd);

    public ulong InUseAt(double position) => Memory.InUseAt(position);

    public ulong PeakInUseDuring(double positionStart, double positionEnd) => Memory.PeakInUseDuring(positionStart, positionEnd);

    public IReadOnlyList<TimeTravelSelfAllocation> SelfAllocations()
    {
        var totals = new Dictionary<(int Thread, int Depth, int Index), ulong>();

        for (var thread = 0; thread < Threads.Count; thread++)
        {
            var rows = Threads[thread].Rows;

            foreach (var (position, bytes) in Memory.AllocationsOf(Threads[thread].ThreadId))
            {
                for (var depth = rows.Count - 1; depth >= 0; depth--)
                {
                    var index = rows[depth].IndexAt(TimeTravelTimelineAxis.Position, position, 0);

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

        return [.. totals.OrderBy(t => t.Key).Select(t => new TimeTravelSelfAllocation(t.Key.Thread, t.Key.Depth, t.Key.Index, t.Value))];
    }

    public int ParentOf(int node) => node >= 0 && node < Parents.Length ? Parents[node] : -1;

    public TimeTravelTimeline Where(Func<int, bool> include) => new([.. Threads.Select(t => t.Where(include))], this);

    public TimeTravelTimeline? RootedAt(Func<CallStackNode, bool> isRoot)
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

        var rooted = Threads.Select(t => Regroup(t, (row, index) => row.NodeAt(index) is var n
                                                                    && n >= 0
                                                                    && n < depths.Length
                                                                    && rootDepths[n] >= 0
                                                                        ? depths[n] - rootDepths[n]
                                                                        : -1))
                            .ToList();

        var windows = Windows(rooted.Where(t => t.Calls > 0 && t.Rows.Count > 0).Select(t => t.Rows[0]));

        var threads = new List<TimeTravelTimelineThread>();

        for (var lane = 0; lane < Threads.Count; lane++)
        {
            var thread = rooted[lane].Calls > 0 ? rooted[lane] : During(Threads[lane], windows, depths);

            if (thread.Calls > 0)
            {
                threads.Add(thread);
            }
        }

        return new TimeTravelTimeline(threads, this);
    }

    public TimeTravelTimeline WithoutCallsUnder(Func<CallStackNode, bool> boundary)
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

    public static double PositionOf(ulong sequence, ulong steps, double stepsPerSequence) => sequence + steps / stepsPerSequence;

    internal void SetMemory(IEnumerable<TimeTravelAllocation> allocations, IEnumerable<TimeTravelFree> frees)
        => Memory = TimeTravelMemoryIndex.Build(allocations, frees);

    internal void MapNodes(CallStackNode?[] nodes) => Nodes = nodes;

    private static List<(double Start, double End)> Windows(IEnumerable<TimeTravelTimelineRow> roots)
    {
        var windows = new List<(double Start, double End)>();

        foreach (var row in roots)
        {
            var starts = row.Starts(TimeTravelTimelineAxis.Position);

            var ends = row.Ends(TimeTravelTimelineAxis.Position);

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

    private static TimeTravelTimelineThread During(TimeTravelTimelineThread thread,
                                                   List<(double Start, double End)> windows,
                                                   int[] depths)
    {
        bool Overlaps(TimeTravelTimelineRow row, int index)
        {
            var start = row.Starts(TimeTravelTimelineAxis.Position)[index];

            var end = row.Ends(TimeTravelTimelineAxis.Position)[index];

            var low = 0;

            var high = windows.Count;

            while (low < high)
            {
                var middle = low + (high - low) / 2;

                if (windows[middle].End < start)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low < windows.Count && windows[low].Start <= end;
        }

        int DepthOf(TimeTravelTimelineRow row, int index)
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
            ? new TimeTravelTimelineThread(thread.ThreadId, [], 0)
            : Regroup(thread, (row, index) => Overlaps(row, index) ? DepthOf(row, index) - shallowest : -1);
    }

    private static TimeTravelTimelineThread Regroup(TimeTravelTimelineThread thread, Func<TimeTravelTimelineRow, int, int> depthOf)
    {
        var counts = new List<int>();

        foreach (var row in thread.Rows)
        {
            for (var index = 0; index < row.Count; index++)
            {
                var depth = depthOf(row, index);

                if (depth < 0)
                {
                    continue;
                }

                while (counts.Count <= depth)
                {
                    counts.Add(0);
                }

                counts[depth]++;
            }
        }

        var rows = counts.Select(c => new RowArrays(c)).ToArray();

        foreach (var row in thread.Rows)
        {
            var positionStarts = row.Starts(TimeTravelTimelineAxis.Position);

            var positionEnds = row.Ends(TimeTravelTimelineAxis.Position);

            var instructionStarts = row.Starts(TimeTravelTimelineAxis.Instructions);

            var instructionEnds = row.Ends(TimeTravelTimelineAxis.Instructions);

            for (var index = 0; index < row.Count; index++)
            {
                var depth = depthOf(row, index);

                if (depth < 0)
                {
                    continue;
                }

                var call = row.CallAt(index);

                rows[depth].Add(positionStarts[index],
                                positionEnds[index],
                                instructionStarts[index],
                                instructionEnds[index],
                                row.NodeAt(index),
                                call < 0 ? uint.MaxValue : (uint)call,
                                (byte)row.Span(TimeTravelTimelineAxis.Position, index).Flags);
            }
        }

        return new TimeTravelTimelineThread(thread.ThreadId, [.. rows.Select(r => r.Build())], counts.Sum());
    }

    internal sealed class Builder
    {
        private Dictionary<uint, ThreadSpans> Threads { get; } = [];

        public void Add(IntPtr spans, int count)
        {
            var size = count * Marshal.SizeOf<TimeTravelCallSpan>();

            var buffer = ArrayPool<byte>.Shared.Rent(size);

            try
            {
                Marshal.Copy(spans, buffer, 0, size);

                Add(MemoryMarshal.Cast<byte, TimeTravelCallSpan>(buffer.AsSpan(0, size)));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public void Add(ReadOnlySpan<TimeTravelCallSpan> spans)
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

        public TimeTravelTimeline Build(TimeTravelCallNode[] nodes)
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
                                 .OrderBy(t => t.StartOf(TimeTravelTimelineAxis.Position))
                                 .ToList();

            return new TimeTravelTimeline(threads, parents, stepsPerSequence);
        }
    }

    private sealed class ThreadSpans
    {
        private const int ChunkShift = 12;

        private const int ChunkSize = 1 << ChunkShift;

        private const int ChunkMask = ChunkSize - 1;

        public ulong MaximumSteps { get; private set; }

        private List<TimeTravelCallSpan[]?> Chunks { get; } = [];

        private int Count { get; set; }

        public void Add(TimeTravelCallSpan span)
        {
            if ((Count & ChunkMask) == 0)
            {
                Chunks.Add(new TimeTravelCallSpan[ChunkSize]);
            }

            Chunks[^1]![Count & ChunkMask] = span;

            Count++;

            MaximumSteps = Math.Max(MaximumSteps, Math.Max(span.StartSteps, span.EndSteps));
        }

        public TimeTravelTimelineThread Build(uint threadId, int[] depths, double stepsPerSequence)
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

            return new TimeTravelTimelineThread(threadId, [.. rows.Select(r => r.Build())], count);
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

        public TimeTravelTimelineRow Build()
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

            return new TimeTravelTimelineRow(PositionStarts,
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
}
