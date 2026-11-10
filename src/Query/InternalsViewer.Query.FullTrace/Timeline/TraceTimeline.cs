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

    internal CallTreeNode?[] MappedNodes => Nodes;

    private int[] Parents { get; }

    private CallTreeNode?[] Nodes { get; set; } = [];

    private TraceCallStore? Calls { get; set; }

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

    public CallTreeNode? GetNode(int callNodeIndex) => callNodeIndex >= 0 && callNodeIndex < Nodes.Length ? Nodes[callNodeIndex] : null;

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
        if (GetNode(callNodeIndex) is not { } callNode)
        {
            return null;
        }

        var depth = GetDepth(callNodeIndex);

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
                    if (GetNode(row.GetCallNodeIndex(index)) is { } node)
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

    public int GetParent(int callNodeIndex) => callNodeIndex >= 0 && callNodeIndex < Parents.Length ? Parents[callNodeIndex] : -1;

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

    internal void RemoveCallsUnder(Func<CallTreeNode, bool> boundary)
    {
        var removed = new bool[NodeCount];

        var any = false;

        for (var callNodeIndex = 0; callNodeIndex < removed.Length; callNodeIndex++)
        {
            var parent = Parents[callNodeIndex];

            removed[callNodeIndex] = (parent >= 0 && removed[parent]) || (GetNode(callNodeIndex) is { } callNode && boundary(callNode));

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

    internal void MapNodes(CallTreeNode?[] nodes) => Nodes = nodes;

    internal void SetCalls(TraceCallStore calls) => Calls = calls;

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
            if ((span.Row.GetFlags(span.Index) & TraceSpanFlags.StartUnknown) != 0)
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

    private int GetDepth(int callNodeIndex)
    {
        var depth = 0;

        for (var current = callNodeIndex >= 0 && callNodeIndex < Parents.Length ? Parents[callNodeIndex] : -1; current >= 0; current = Parents[current])
        {
            depth++;
        }

        return depth;
    }
}
