using System.Runtime.InteropServices;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed class TraceReads
{
    private const string Module = "sqlmin";

    private const string BufferPool = "BPool";

    private const string GetMethod = "Get";

    private const string ReadAheadMethod = "ReadAhead";

    private const int PageCountSlot = 3;

    private TraceReads(TraceRead[] reads)
    {
        Reads = reads;

        Positions = AxisReads.Build(reads, TraceTimelineAxis.Position);

        Instructions = AxisReads.Build(reads, TraceTimelineAxis.Instructions);
    }

    public static TraceReads Empty { get; } = new([]);

    public int Count => Reads.Length;

    public IReadOnlyList<TraceRead> All => Reads;

    private TraceRead[] Reads { get; }

    private AxisReads Positions { get; }

    private AxisReads Instructions { get; }

    public ReadOnlySpan<double> GetStarts(TraceTimelineAxis axis) => Of(axis).Starts;

    public int GetFirstEndingFrom(TraceTimelineAxis axis, double value) => SortedSearch.FirstAtOrAfter(Of(axis).RunningEnds, value);

    public TraceRead GetRead(TraceTimelineAxis axis, int index) => Reads[Of(axis).Order[index]];

    public TraceRead? GetNearest(TraceTimelineAxis axis, double value, double tolerance)
    {
        var reads = Of(axis);

        TraceRead? nearest = null;

        var best = double.MaxValue;

        for (var index = SortedSearch.FirstAfter(reads.Starts, value + tolerance) - 1;
             index >= 0 && reads.RunningEnds[index] >= value - tolerance;
             index--)
        {
            var read = Reads[reads.Order[index]];

            var span = read.Of(axis);

            var distance = value < span.Start ? span.Start - value : value > span.End ? value - span.End : 0;

            if (distance <= tolerance && distance < best)
            {
                nearest = read;

                best = distance;
            }
        }

        return nearest;
    }

    internal static TraceReads Build(TraceTimeline timeline, TraceCallStore calls)
    {
        var reads = new List<TraceRead>();

        var froms = new List<TracePosition>();

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (calls.GetCallList(address, instance) is not { Count: > 0 } list
                || GetReadType(calls.GetNode(list.GetNodeIndex(0))?.Frame) is not { } type)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                if (GetPage(call) is not { } page || timeline.FindCall(address, instance, index) is not { } location)
                {
                    continue;
                }

                var pageCount = type == TraceReadType.ReadAhead && call.IntegerSlots.Length > PageCountSlot
                    ? (int)call.IntegerSlots[PageCountSlot]
                    : 1;

                reads.Add(new TraceRead(type,
                                        page,
                                        pageCount,
                                        call.ThreadId,
                                        location.Node,
                                        location.Call,
                                        location.Span(TraceTimelineAxis.Position),
                                        location.Span(TraceTimelineAxis.Instructions)));

                froms.Add(call.FromPosition);
            }
        }

        var operators = GetOperators(calls, timeline.GetOperatorsByInstance(), reads, froms);

        return new TraceReads([.. reads.Select((r, i) => operators[i] is { } found ? r with { Operator = found } : r)]);
    }

    private static TraceReadType? GetReadType(CallstackFrame? frame)
        => frame is { Resolved: { ClassName: BufferPool } resolved } && frame.Module.Equals(Module, StringComparison.OrdinalIgnoreCase)
            ? resolved.MethodName switch
            {
                GetMethod => TraceReadType.Page,
                ReadAheadMethod => TraceReadType.ReadAhead,
                _ => null
            }
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

    private static ExecutionOperatorEvent?[] GetOperators(TraceCallStore calls,
                                                          Dictionary<ulong, ExecutionOperatorEvent> owners,
                                                          List<TraceRead> reads,
                                                          List<TracePosition> froms)
    {
        var operators = new ExecutionOperatorEvent?[reads.Count];

        if (owners.Count == 0)
        {
            return operators;
        }

        var threads = new Dictionary<uint, List<SweepEntry>>();

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (!owners.ContainsKey(instance) || calls.GetCallList(address, instance) is not { Count: > 0 } list)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                Add(list.GetThread(index), new SweepEntry(list.GetFromPosition(index), list.GetToPosition(index), instance, -1));
            }
        }

        for (var index = 0; index < reads.Count; index++)
        {
            Add(reads[index].Thread, new SweepEntry(froms[index], froms[index], 0, index));
        }

        var open = new Stack<SweepEntry>();

        foreach (var thread in threads.Values)
        {
            thread.Sort((left, right) => left.From != right.From ? left.From.CompareTo(right.From) : right.To.CompareTo(left.To));

            open.Clear();

            foreach (var entry in thread)
            {
                while (open.TryPeek(out var outer) && outer.To <= entry.From)
                {
                    open.Pop();
                }

                if (entry.Read >= 0)
                {
                    operators[entry.Read] = open.TryPeek(out var enclosing) ? owners[enclosing.Instance] : null;

                    continue;
                }

                open.Push(entry);
            }
        }

        return operators;

        void Add(uint thread, SweepEntry entry)
        {
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, thread, out _);

            (list ??= []).Add(entry);
        }
    }

    private AxisReads Of(TraceTimelineAxis axis) => axis == TraceTimelineAxis.Position ? Positions : Instructions;

    private readonly record struct SweepEntry(TracePosition From, TracePosition To, ulong Instance, int Read);

    private sealed record AxisReads(double[] Starts, double[] RunningEnds, int[] Order)
    {
        public static AxisReads Build(TraceRead[] reads, TraceTimelineAxis axis)
        {
            int[] order = [.. Enumerable.Range(0, reads.Length).OrderBy(i => reads[i].Of(axis).Start)];

            var runningEnds = new double[order.Length];

            var end = double.MinValue;

            for (var index = 0; index < order.Length; index++)
            {
                end = Math.Max(end, reads[order[index]].Of(axis).End);

                runningEnds[index] = end;
            }

            return new AxisReads([.. order.Select(i => reads[i].Of(axis).Start)], runningEnds, order);
        }
    }
}
