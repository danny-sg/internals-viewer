using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed class TraceTickSeries
{
    private TraceTickSeries(TraceTick[] ticks)
    {
        Ticks = ticks;

        ByOperator = OperatorMarks<TraceTick>.Build(ticks, t => t.Operator, t => t.Thread);
    }

    public static TraceTickSeries Empty { get; } = new([]);

    public int Count => Ticks.Length;

    public IReadOnlyList<TraceTick> All => Ticks;

    private TraceTick[] Ticks { get; }

    private OperatorMarks<TraceTick> ByOperator { get; }

    public TraceMarkIndex<TraceTick>? GetTicks(ExecutionOperatorEvent operatorEvent, uint thread) => ByOperator.Get(operatorEvent, thread);

    public static TraceTickSeries FromCalls(TraceTimeline timeline,
                                            TraceCallStore calls,
                                            TraceOperatorLifetimes lifetimes,
                                            Func<CallstackFrame, bool> isFunction,
                                            Func<CallstackFrame, TraceArgumentCall, byte?> getValue)
        => Build(timeline, calls, lifetimes, n => n.Frame is { } frame && isFunction(frame), getValue, null, null);

    public static TraceTickSeries FromDefinition(TraceTimeline timeline,
                                                 TraceCallStore calls,
                                                 TraceOperatorLifetimes lifetimes,
                                                 TraceTickDefinition definition)
    {
        var nodes = new Dictionary<CallStackNode, bool>(ReferenceEqualityComparer.Instance);

        return Build(timeline,
                     calls,
                     lifetimes,
                     definition.IsFunction,
                     (_, call) => definition.MatchesCall(call) ? (byte)0 : null,
                     node => nodes.TryGetValue(node, out var isMatch) ? isMatch : nodes[node] = definition.MatchesNode(node),
                     definition.MatchesOperator);
    }

    internal static TraceTickSeries Create(IEnumerable<TraceTick> ticks) => new([.. ticks]);

    private static TraceTickSeries Build(TraceTimeline timeline,
                                         TraceCallStore calls,
                                         TraceOperatorLifetimes lifetimes,
                                         Func<CallStackNode, bool> isFunction,
                                         Func<CallstackFrame, TraceArgumentCall, byte?> getValue,
                                         Func<CallStackNode, bool>? isNode,
                                         Func<ExecutionOperatorEvent?, bool>? isOperator)
    {
        var ticks = new List<TraceTick>();

        var points = new List<SweepSpan<TracePosition>>();

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (calls.GetCallList(address, instance) is not { Count: > 0 } list
                || calls.GetNode(list.GetNodeIndex(0)) is not { Frame: { } frame } first
                || !isFunction(first))
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                if (getValue(frame, call) is not { } value
                    || timeline.FindCall(address, instance, index) is not { } location
                    || (isNode is not null && !isNode(location.Node)))
                {
                    continue;
                }

                ticks.Add(new TraceTick(value,
                                        call.ThreadId,
                                        location.Node,
                                        location.Call,
                                        location.Span(TraceTimelineAxis.Position),
                                        location.Span(TraceTimelineAxis.Instructions))
                {
                    Function = address,
                    Instance = instance,
                    Index = index
                });

                points.Add(new SweepSpan<TracePosition>(call.ThreadId, call.FromPosition, call.FromPosition, 0));
            }
        }

        var operators = GetEnclosingOperators(calls, lifetimes.OperatorsByInstance, points);

        return new TraceTickSeries([.. ticks.Select((t, i) => operators[i] is { } found ? t with { Operator = found } : t)
                                            .Where(t => isOperator is null || isOperator(t.Operator))]);
    }

    private static ExecutionOperatorEvent?[] GetEnclosingOperators(TraceCallStore calls,
                                                                   IReadOnlyDictionary<ulong, ExecutionOperatorEvent> owners,
                                                                   IReadOnlyList<SweepSpan<TracePosition>> points)
    {
        if (owners.Count == 0)
        {
            return new ExecutionOperatorEvent?[points.Count];
        }

        var spans = new List<SweepSpan<TracePosition>>();

        foreach (var (address, instance) in calls.FunctionKeys)
        {
            if (!owners.ContainsKey(instance) || calls.GetCallList(address, instance) is not { Count: > 0 } list)
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                spans.Add(new SweepSpan<TracePosition>(list.GetThread(index),
                                                       list.GetFromPosition(index),
                                                       list.GetToPosition(index),
                                                       instance));
            }
        }

        return [.. EnclosingSweep.GetEnclosing(spans, points).Select(instance => owners.GetValueOrDefault(instance))];
    }
}
