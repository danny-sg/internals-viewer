using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline.Ticks;

public sealed class TraceTickSeries
{
    private TraceTickSeries(Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceTickIndex>> indexes)
    {
        Indexes = indexes;

        Count = indexes.Values.Sum(t => t.Values.Sum(i => i.Count));

        Bytes = indexes.Values.Sum(t => t.Values.Sum(i => i.Bytes));
    }

    public static TraceTickSeries Empty { get; }
        = new(new Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceTickIndex>>(ReferenceEqualityComparer.Instance));

    public int Count { get; }

    public long Bytes { get; }

    public IEnumerable<TraceTick> All => Indexes.Values.SelectMany(t => t.Values).SelectMany(i => i.All);

    private Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceTickIndex>> Indexes { get; }

    public TraceTickIndex? GetTicks(ExecutionOperatorEvent operatorEvent, uint thread)
        => Indexes.TryGetValue(operatorEvent, out var threads) ? threads.GetValueOrDefault(thread) : null;

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
        var nodes = new Dictionary<CallTreeNode, bool>(ReferenceEqualityComparer.Instance);

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
                                              Func<CallFrame, bool> isFunction,
                                              Func<CallFrame, TraceArgumentCall, byte?> getValue,
                                              EnclosingOperatorCallIndex enclosing)
        => Build(timeline, calls, lifetimes, enclosing, n => n.Frame is { } frame && isFunction(frame), getValue, null, null);

    internal static TraceTickSeries FromSources(TraceTimeline timeline,
                                                List<TraceTickSource> sources,
                                                TraceOperatorLifetimeList lifetimes,
                                                EnclosingOperatorCallIndex enclosing,
                                                Func<ExecutionOperatorEvent?, bool>? isOperator = null)
    {
        var points = new List<SweepSpan<TracePosition>>(sources.Count);

        foreach (var source in sources)
        {
            points.Add(new SweepSpan<TracePosition>(source.Thread.ThreadId, source.From.Position, source.From.Position, 0));
        }

        var owners = lifetimes.OperatorsByInstance;

        var instances = enclosing.GetEnclosing(points, includeRowReturns: true);

        var owned = new List<TraceTickSource>(sources.Count);

        for (var index = 0; index < sources.Count; index++)
        {
            if (owners.GetValueOrDefault(instances[index]) is { } operatorEvent && (isOperator is null || isOperator(operatorEvent)))
            {
                owned.Add(sources[index] with { Operator = operatorEvent });
            }
        }

        return Create(timeline, owned);
    }

    internal static TraceTickSeries Create(TraceTimeline timeline, IEnumerable<TraceTickSource> sources)
    {
        var grouped = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, List<TraceTickSource>>>(ReferenceEqualityComparer.Instance);

        foreach (var source in sources)
        {
            if (source.Operator is not { } operatorEvent)
            {
                continue;
            }

            ref var threads = ref CollectionsMarshal.GetValueRefOrAddDefault(grouped, operatorEvent, out _);

            threads ??= [];

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(threads, source.Thread.ThreadId, out _);

            (list ??= []).Add(source);
        }

        var indexes = new Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceTickIndex>>(ReferenceEqualityComparer.Instance);

        foreach (var (operatorEvent, threads) in grouped)
        {
            indexes[operatorEvent] = threads.ToDictionary(t => t.Key,
                                                          t => new TraceTickIndex(timeline, t.Value[0].Thread, operatorEvent, t.Value));
        }

        return new TraceTickSeries(indexes);
    }

    private static TraceTickSeries Build(TraceTimeline timeline,
                                         TraceCallStore calls,
                                         TraceOperatorLifetimeList lifetimes,
                                         EnclosingOperatorCallIndex enclosing,
                                         Func<CallTreeNode, bool> isFunction,
                                         Func<CallFrame, TraceArgumentCall, byte?> getValue,
                                         Func<CallTreeNode, bool>? isNode,
                                         Func<ExecutionOperatorEvent?, bool>? isOperator)
    {
        var sources = new List<TraceTickSource>();

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

                sources.Add(TraceTickSource.FromCall(location, value));
            }
        }

        return FromSources(timeline, sources, lifetimes, enclosing, isOperator);
    }
}
