using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceRowBufferMarks
{
    private TraceRowBufferMarks(Dictionary<ExecutionOperatorEvent, OperatorMarks> rowBuffers,
                                Dictionary<ExecutionOperatorEvent, OperatorMarks> batches)
    {
        RowBuffers = rowBuffers;

        Batches = batches;

        Count = rowBuffers.Values.Sum(t => t.Count) + batches.Values.Sum(t => t.Count);
    }

    public static TraceRowBufferMarks Empty { get; }
        = new(new(ReferenceEqualityComparer.Instance), new(ReferenceEqualityComparer.Instance));

    public int Count { get; }

    private Dictionary<ExecutionOperatorEvent, OperatorMarks> RowBuffers { get; }

    private Dictionary<ExecutionOperatorEvent, OperatorMarks> Batches { get; }

    public bool HasMarks(ExecutionOperatorEvent operatorEvent)
        => RowBuffers.ContainsKey(operatorEvent) || Batches.ContainsKey(operatorEvent);

    public ReadOnlySpan<double> GetMarks(ExecutionOperatorEvent operatorEvent, TraceTimelineAxis axis)
        => GetMarks(RowBuffers, operatorEvent, axis);

    public ReadOnlySpan<double> GetBatchMarks(ExecutionOperatorEvent operatorEvent, TraceTimelineAxis axis)
        => GetMarks(Batches, operatorEvent, axis);

    public (CallStackNode Node, int Call, bool IsBatch)? GetNearestMark(ExecutionOperatorEvent operatorEvent,
                                                                       TraceTimelineAxis axis,
                                                                       double value)
    {
        var rowBuffer = GetNearest(RowBuffers, operatorEvent, axis, value);

        var batch = GetNearest(Batches, operatorEvent, axis, value);

        if (batch is { } nearestBatch && (rowBuffer is not { } nearestRow || nearestBatch.Distance < nearestRow.Distance))
        {
            return (nearestBatch.Node, nearestBatch.Call, true);
        }

        return rowBuffer is { } found ? (found.Node, found.Call, false) : null;
    }

    internal static TraceRowBufferMarks Build(TraceTimeline timeline, RowBufferStore rowBuffers, BatchStore? batches = null)
    {
        batches ??= BatchStore.Empty;

        if (rowBuffers.Count == 0 && batches.Count == 0)
        {
            return Empty;
        }

        var owners = timeline.GetOperatorsByInstance();

        var captures = new Dictionary<(uint Thread, ulong Iterator), List<Capture>>();

        var batchCaptures = new Dictionary<ExecutionOperatorEvent, List<Capture>>(ReferenceEqualityComparer.Instance);

        foreach (var batch in batches.All)
        {
            if (owners.GetValueOrDefault(batch.Instance) is not { } producer
                || timeline.FindCall(batch.Function, batch.Instance, batch.Call) is not { } location)
            {
                continue;
            }

            ref var batchList = ref CollectionsMarshal.GetValueRefOrAddDefault(batchCaptures, producer, out _);

            (batchList ??= []).Add(GetCapture(location));
        }

        foreach (var row in rowBuffers.All)
        {
            if (timeline.FindCall(row.Function, row.Instance, row.Call) is not { } location)
            {
                continue;
            }

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(captures, (location.Thread.ThreadId, row.Iterator), out _);

            (list ??= []).Add(GetCapture(location));
        }

        var operators = new Dictionary<ExecutionOperatorEvent, List<Capture>>(ReferenceEqualityComparer.Instance);

        foreach (var ((_, iterator), list) in captures)
        {
            if (GetOwner(owners, batches, iterator) is not { } owner)
            {
                continue;
            }

            ref var marks = ref CollectionsMarshal.GetValueRefOrAddDefault(operators, owner, out _);

            (marks ??= []).AddRange(GetInnermost(list));
        }

        return new TraceRowBufferMarks(Build(operators), Build(batchCaptures));
    }

    private static Dictionary<ExecutionOperatorEvent, OperatorMarks> Build(Dictionary<ExecutionOperatorEvent, List<Capture>> captures)
    {
        var marks = new Dictionary<ExecutionOperatorEvent, OperatorMarks>(captures.Count, ReferenceEqualityComparer.Instance);

        foreach (var (operatorEvent, list) in captures)
        {
            marks[operatorEvent] = OperatorMarks.Build(list);
        }

        return marks;
    }

    private static Capture GetCapture(TraceSpanLocation location)
        => new(location.StartOf(TraceTimelineAxis.Position),
               location.EndOf(TraceTimelineAxis.Position),
               location.EndOf(TraceTimelineAxis.Instructions),
               location.Node,
               location.Call);

    private static ExecutionOperatorEvent? GetOwner(Dictionary<ulong, ExecutionOperatorEvent> owners, BatchStore batches, ulong iterator)
    {
        if (owners.TryGetValue(iterator, out var owner))
        {
            return owner;
        }

        return batches.GetProducer(iterator) is { } producer ? owners.GetValueOrDefault(producer) : null;
    }

    private static ReadOnlySpan<double> GetMarks(Dictionary<ExecutionOperatorEvent, OperatorMarks> operators,
                                                 ExecutionOperatorEvent operatorEvent,
                                                 TraceTimelineAxis axis)
    {
        return operators.TryGetValue(operatorEvent, out var marks) ? marks.Of(axis).Values : [];
    }

    private static (CallStackNode Node, int Call, double Distance)? GetNearest(Dictionary<ExecutionOperatorEvent, OperatorMarks> operators,
                                                                               ExecutionOperatorEvent operatorEvent,
                                                                               TraceTimelineAxis axis,
                                                                               double value)
    {
        if (!operators.TryGetValue(operatorEvent, out var marks))
        {
            return null;
        }

        var (values, nodes, calls) = marks.Of(axis);

        var index = SortedSearch.FirstAtOrAfter(values, value);

        if (index > 0 && (index == values.Length || value - values[index - 1] <= values[index] - value))
        {
            index--;
        }

        return index < values.Length ? (nodes[index], calls[index], Math.Abs(values[index] - value)) : null;
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

    private sealed record AxisMarks(double[] Values, CallStackNode[] Nodes, int[] Calls)
    {
        public static AxisMarks Build(List<Capture> captures, Func<Capture, double> getValue)
        {
            Capture[] ordered = [.. captures.OrderBy(getValue)];

            return new AxisMarks([.. ordered.Select(getValue)], [.. ordered.Select(c => c.Node)], [.. ordered.Select(c => c.Call)]);
        }
    }

    private sealed record OperatorMarks(AxisMarks Positions, AxisMarks Instructions)
    {
        public int Count => Positions.Values.Length;

        public AxisMarks Of(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Positions : Instructions;

        public static OperatorMarks Build(List<Capture> captures)
            => new(AxisMarks.Build(captures, c => c.End), AxisMarks.Build(captures, c => c.InstructionEnd));
    }
}
