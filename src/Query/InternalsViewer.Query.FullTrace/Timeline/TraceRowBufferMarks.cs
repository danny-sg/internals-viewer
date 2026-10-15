using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceRowBufferMarks
{
    private TraceRowBufferMarks(Dictionary<uint, ThreadMarks> threads, Dictionary<uint, ThreadMarks> batchThreads)
    {
        Threads = threads;

        BatchThreads = batchThreads;

        Count = threads.Values.Sum(t => t.Positions.Length) + batchThreads.Values.Sum(t => t.Positions.Length);
    }

    public static TraceRowBufferMarks Empty { get; } = new([], []);

    public int Count { get; }

    private Dictionary<uint, ThreadMarks> Threads { get; }

    private Dictionary<uint, ThreadMarks> BatchThreads { get; }

    public ReadOnlySpan<double> GetMarks(uint threadId, TraceTimelineAxis axis) => GetMarks(Threads, threadId, axis);

    public ReadOnlySpan<double> GetBatchMarks(uint threadId, TraceTimelineAxis axis) => GetMarks(BatchThreads, threadId, axis);

    public (CallStackNode Node, int Call)? GetMarkAt(uint threadId, TraceTimelineAxis axis, double value, double tolerance)
    {
        (CallStackNode Node, int Call)? nearest = null;

        var distance = tolerance;

        FindNearest(Threads, threadId, axis, value, ref nearest, ref distance);

        FindNearest(BatchThreads, threadId, axis, value, ref nearest, ref distance);

        return nearest;
    }

    internal static TraceRowBufferMarks Build(TraceTimeline timeline, RowBufferIndex rowBuffers, BatchIndex? batches = null)
    {
        batches ??= BatchIndex.Empty;

        if (rowBuffers.Count == 0 && batches.Count == 0)
        {
            return Empty;
        }

        var captures = new Dictionary<(uint Thread, ulong Iterator), List<Capture>>();

        var batchCaptures = new Dictionary<uint, List<Capture>>();

        foreach (var location in timeline.GetSpanLocations())
        {
            if (location.Call < 0 || location.Node.Frame is not { } frame)
            {
                continue;
            }

            var capture = new Capture(location.StartOf(TraceTimelineAxis.Position),
                                      location.EndOf(TraceTimelineAxis.Position),
                                      location.EndOf(TraceTimelineAxis.Instructions),
                                      location.Node,
                                      location.Call);

            if (batches.HasBatch(frame.Address, frame.Instance, location.Call))
            {
                ref var batchList = ref CollectionsMarshal.GetValueRefOrAddDefault(batchCaptures, location.Thread.ThreadId, out _);

                (batchList ??= []).Add(capture);
            }

            if (rowBuffers.GetRow(frame.Address, frame.Instance, location.Call) is not { } row)
            {
                continue;
            }

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(captures, (location.Thread.ThreadId, row.Iterator), out _);

            (list ??= []).Add(capture);
        }

        var threads = new Dictionary<uint, List<Capture>>();

        foreach (var ((thread, _), list) in captures)
        {
            ref var marks = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, thread, out _);

            (marks ??= []).AddRange(GetInnermost(list));
        }

        return new TraceRowBufferMarks(threads.ToDictionary(t => t.Key, t => ThreadMarks.Build(t.Value)),
                                       batchCaptures.ToDictionary(t => t.Key, t => ThreadMarks.Build(t.Value)));
    }

    private static ReadOnlySpan<double> GetMarks(Dictionary<uint, ThreadMarks> threads, uint threadId, TraceTimelineAxis axis)
    {
        if (!threads.TryGetValue(threadId, out var marks))
        {
            return [];
        }

        return axis == TraceTimelineAxis.Position ? marks.Positions : marks.Instructions;
    }

    private static void FindNearest(Dictionary<uint, ThreadMarks> threads,
                                    uint threadId,
                                    TraceTimelineAxis axis,
                                    double value,
                                    ref (CallStackNode Node, int Call)? nearest,
                                    ref double distance)
    {
        if (!threads.TryGetValue(threadId, out var marks))
        {
            return;
        }

        var values = axis == TraceTimelineAxis.Position ? marks.Positions : marks.Instructions;

        var end = value + distance;

        for (var index = SortedSearch.FirstAtOrAfter(values, value - distance); index < values.Length && values[index] <= end; index++)
        {
            var gap = Math.Abs(values[index] - value);

            if (gap <= distance)
            {
                distance = gap;

                nearest = (marks.Nodes[index], marks.Calls[index]);
            }
        }
    }

    private static List<Capture> GetInnermost(List<Capture> captures)
    {
        captures.Sort((left, right) => left.End != right.End ? left.End.CompareTo(right.End) : right.Start.CompareTo(left.Start));

        var innermost = new List<Capture>(captures.Count);

        foreach (var capture in captures)
        {
            if (innermost.Count > 0 && innermost[^1] is var inner && inner.Start >= capture.Start && inner.End <= capture.End)
            {
                continue;
            }

            innermost.Add(capture);
        }

        return innermost;
    }

    private readonly record struct Capture(double Start, double End, double InstructionEnd, CallStackNode Node, int Call);

    private sealed record ThreadMarks(double[] Positions, double[] Instructions, CallStackNode[] Nodes, int[] Calls)
    {
        public static ThreadMarks Build(List<Capture> captures)
        {
            captures.Sort((left, right) => left.End.CompareTo(right.End));

            return new ThreadMarks([.. captures.Select(c => c.End)],
                                   [.. captures.Select(c => c.InstructionEnd)],
                                   [.. captures.Select(c => c.Node)],
                                   [.. captures.Select(c => c.Call)]);
        }
    }
}
