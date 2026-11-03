using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed class TraceTickSeries
{
    private TraceTickSeries(TraceTick[] ticks, TraceScale scale)
    {
        Ticks = ticks;

        ByOperator = OperatorMarkMap<TraceTick>.Build(ticks, scale, t => t.Operator, t => t.Thread);
    }

    public static TraceTickSeries Empty { get; } = new([], TraceScale.Default);

    public int Count => Ticks.Length;

    public IReadOnlyList<TraceTick> All => Ticks;

    private TraceTick[] Ticks { get; }

    private OperatorMarkMap<TraceTick> ByOperator { get; }

    public TraceMarkIndex<TraceTick>? GetTicks(ExecutionOperatorEvent operatorEvent, uint thread) => ByOperator.Get(operatorEvent, thread);

    public static TraceTickSeries FromDefinition(TraceResult trace, TraceTickDefinition definition)
        => FromDefinition(trace.Timeline, trace.CallStore, trace.Lifetimes, definition, trace.EnclosingCalls);

    public static TraceTickSeries FromDefinition(TraceTimeline timeline,
                                                 TraceCallStore calls,
                                                 TraceOperatorLifetimeList lifetimes,
                                                 TraceTickDefinition definition)
        => FromDefinition(timeline, calls, lifetimes, definition, EnclosingOperatorCallIndex.Build(calls, lifetimes.OperatorsByInstance));

    internal static TraceTickSeries FromDefinition(TraceTimeline timeline,
                                                   TraceCallStore calls,
                                                   TraceOperatorLifetimeList lifetimes,
                                                   TraceTickDefinition definition,
                                                   EnclosingOperatorCallIndex enclosing)
    {
        var nodes = new Dictionary<CallStackNode, bool>(ReferenceEqualityComparer.Instance);

        return Build(timeline,
                     calls,
                     lifetimes,
                     enclosing,
                     definition.IsFunction,
                     (_, call) => definition.MatchesCall(call) ? (byte)0 : null,
                     node => nodes.TryGetValue(node, out var isMatch) ? isMatch : nodes[node] = definition.MatchesNode(node),
                     definition.MatchesOperator);
    }

    internal static TraceTickSeries FromCalls(TraceTimeline timeline,
                                              TraceCallStore calls,
                                              TraceOperatorLifetimeList lifetimes,
                                              Func<CallstackFrame, bool> isFunction,
                                              Func<CallstackFrame, TraceArgumentCall, byte?> getValue,
                                              EnclosingOperatorCallIndex enclosing)
        => Build(timeline, calls, lifetimes, enclosing, n => n.Frame is { } frame && isFunction(frame), getValue, null, null);

    internal static TraceTickSeries Create(IEnumerable<TraceTick> ticks, TraceScale scale) => new([.. ticks], scale);

    private static TraceTickSeries Build(TraceTimeline timeline,
                                         TraceCallStore calls,
                                         TraceOperatorLifetimeList lifetimes,
                                         EnclosingOperatorCallIndex enclosing,
                                         Func<CallStackNode, bool> isFunction,
                                         Func<CallstackFrame, TraceArgumentCall, byte?> getValue,
                                         Func<CallStackNode, bool>? isNode,
                                         Func<ExecutionOperatorEvent?, bool>? isOperator)
    {
        var ticks = new List<TraceTick>();

        var points = new List<SweepSpan<TracePosition>>();

        foreach (var list in calls.GetCallLists())
        {
            if (calls.GetFirstNode(list) is not { Frame: { } frame } first || !isFunction(first))
            {
                continue;
            }

            for (var index = 0; index < list.Count; index++)
            {
                var call = list[index];

                if (getValue(frame, call) is not { } value
                    || timeline.FindCall(list.FunctionAddress, list.InstanceAddress, index) is not { } location
                    || (isNode is not null && !isNode(location.Node)))
                {
                    continue;
                }

                ticks.Add(new TraceTick(value, call.ThreadId, location.Node, location.Row, location.Index));

                points.Add(new SweepSpan<TracePosition>(call.ThreadId, call.FromPosition, call.FromPosition, 0));
            }
        }

        var owners = lifetimes.OperatorsByInstance;

        var instances = enclosing.GetEnclosing(points, includeRowReturns: true);

        return new TraceTickSeries([.. ticks.Select((t, i) => owners.GetValueOrDefault(instances[i]) is { } found
                                                                  ? t with { Operator = found }
                                                                  : t)
                                            .Where(t => isOperator is null || isOperator(t.Operator))],
                                   timeline.Scale);
    }
}
