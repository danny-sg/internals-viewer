using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimeline
{
    public const string ShutdownSymbol = "CXStmtQuery::ShutdownNormal";

    internal TraceTimeline(IReadOnlyList<TraceTimelineThread> threads, TraceNodeMap nodes, TraceScale scale)
    {
        Threads = threads;
        Nodes = nodes;
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
        Nodes = source.Nodes;
        Scale = source.Scale;
        SpanCount = threads.Sum(t => t.CallCount);
        Start = source.Start;
        End = source.End;

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

    public TracePoint End { get; }

    public int NodeCount => Nodes.Count;

    public long SpanCount { get; }

    internal TraceNodeMap Nodes { get; }

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

    public CallTreeNode? GetNode(int replayNodeIndex) => Nodes.GetNode(replayNodeIndex);

    public TraceSpanLocation? FindSpan(TraceCallList calls, int callIndex)
        => callIndex >= 0 && callIndex < calls.Count
            ? Find(calls.GetThreadId(callIndex), calls.GetFromPosition(callIndex), calls.GetReplayNodeIndex(callIndex), callIndex)
            : null;

    public TraceSpanLocation? FindSpan(uint thread, TraceSpan span)
        => Find(thread, span.From.Position, span.ReplayNodeIndex, span.CallIndex);

    private TraceSpanLocation? Find(uint thread, TracePosition from, int replayNodeIndex, int callIndex)
    {
        if (GetNode(replayNodeIndex) is not { } callNode)
        {
            return null;
        }

        var depth = GetDepth(replayNodeIndex);

        for (var lane = 0; lane < Threads.Count; lane++)
        {
            if (Threads[lane].ThreadId != thread)
            {
                continue;
            }

            var rows = Threads[lane].Rows;

            if (depth < rows.Count && rows[depth].GetIndex(from, replayNodeIndex, callIndex) is >= 0 and var expected)
            {
                return new TraceSpanLocation(lane, Threads[lane], depth, rows[depth], expected, callNode);
            }

            for (var row = 0; row < rows.Count; row++)
            {
                if (row != depth && rows[row].GetIndex(from, replayNodeIndex, callIndex) is >= 0 and var index)
                {
                    return new TraceSpanLocation(lane, Threads[lane], row, rows[row], index, callNode);
                }
            }
        }

        return null;
    }

    public TraceSpan? GetInnermostSpan(uint thread, TracePosition position)
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
                    if (GetNode(row.GetReplayNodeIndex(index)) is { } node)
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

    public int GetParent(int replayNodeIndex) => Nodes.GetParent(replayNodeIndex);

    public (TraceSpan Span, uint ThreadId)? GetFirstRootSpan()
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

    public TracePosition? GetFirstSpanStart(string symbol)
    {
        TracePosition? first = null;

        foreach (var (_, span) in GetSpans(symbol))
        {
            if (first is not { } found || span.From.Position < found)
            {
                first = span.From.Position;
            }
        }

        return first;
    }

    public IEnumerable<(uint ThreadId, TraceSpan Span)> GetSpans(string symbol) => GetSpans(n => n.Symbol == symbol);

    public IEnumerable<(uint ThreadId, TraceSpan Span)> GetSpans(Func<CallTreeNode, bool> predicate)
    {
        var isMatch = new bool[NodeCount];

        var anyMatch = false;

        for (var index = 0; index < isMatch.Length; index++)
        {
            if (GetNode(index) is { } node && predicate(node))
            {
                isMatch[index] = true;
                anyMatch = true;
            }
        }

        if (!anyMatch)
        {
            yield break;
        }

        foreach (var thread in Threads)
        {
            foreach (var row in thread.Rows)
            {
                for (var index = 0; index < row.Count; index++)
                {
                    var replayNodeIndex = row.GetReplayNodeIndex(index);

                    if (replayNodeIndex >= 0 && replayNodeIndex < isMatch.Length && isMatch[replayNodeIndex])
                    {
                        yield return (thread.ThreadId, row.GetSpan(index));
                    }
                }
            }
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

    private int GetDepth(int replayNodeIndex)
    {
        var depth = 0;

        for (var current = GetParent(replayNodeIndex); current >= 0; current = GetParent(current))
        {
            depth++;
        }

        return depth;
    }
}
