using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;

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

    private Dictionary<ExecutionOperatorEvent, Dictionary<uint, TraceTickIndex>> Indexes { get; }

    public TraceTickIndex? GetTicks(ExecutionOperatorEvent operatorEvent, uint thread)
        => Indexes.TryGetValue(operatorEvent, out var threads) ? threads.GetValueOrDefault(thread) : null;

    internal IEnumerable<TraceTickIndex> AllIndexes => Indexes.Values.SelectMany(t => t.Values);

    public IEnumerable<ExecutionOperatorEvent> GetRunning(TracePosition position)
        => Indexes.Where(o => o.Value.Values.Any(i => i.IsRunning(position))).Select(o => o.Key);

    public IEnumerable<ExecutionOperatorEvent> GetEnding(TracePosition after, TracePosition upTo, Func<byte, bool> include)
        => Indexes.Where(o => o.Value.Values.Any(i => i.HasEndBetween(after, upTo, include))).Select(o => o.Key);

    public int[] GetValueCounts()
    {
        var counts = new int[byte.MaxValue + 1];

        foreach (var index in AllIndexes)
        {
            for (var position = 0; position < index.Count; position++)
            {
                counts[index.GetValue(position)]++;
            }
        }

        return counts;
    }

    public static TraceTickSeries FromDefinition(TraceResult trace, TraceTickDefinition definition)
        => definition.Source == TraceTickSourceType.Call
            ? FromDefinition(trace.Timeline, trace.CallStore, trace.Lifetimes, definition, trace.EnclosingCalls)
            : FromSource(trace.Timeline, GetSource(trace, definition.Source), definition);

    public static TraceTickTrackSeries FromRules(TraceResult trace,
                                                 IReadOnlyList<TraceTickDefinition> rules,
                                                 Func<TraceTickDefinition, TraceTickSeries> getSeries)
    {
        if (IsWholeSource(rules))
        {
            return new TraceTickTrackSeries(GetSource(trace, rules[0].Source), [.. rules.Select(r => r.Value)]);
        }

        if (rules.Count == 1)
        {
            return new TraceTickTrackSeries(getSeries(rules[0]), [rules[0].Source == TraceTickSourceType.Call ? (byte)0 : rules[0].Value]);
        }

        var count = Math.Min(rules.Count, byte.MaxValue + 1);

        return new TraceTickTrackSeries(Combine(trace.Timeline, [.. rules.Take(count).Select(getSeries)]),
                                        [.. Enumerable.Range(0, count).Select(i => (byte)i)]);
    }

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

    private static bool IsWholeSource(IReadOnlyList<TraceTickDefinition> rules)
        => rules.Count > 0
           && rules.All(r => r.Source != TraceTickSourceType.Call && r.Source == rules[0].Source && r.Conditions.Count == 0)
           && rules.Select(r => r.Value).Distinct().Count() == rules.Count;

    private static TraceTickSeries GetSource(TraceResult trace, TraceTickSourceType source) => source switch
    {
        TraceTickSourceType.RowReturn => trace.RowReturnMarks.Ticks,
        TraceTickSourceType.RowAccess => trace.RowAccesses,
        TraceTickSourceType.Seek => trace.Seeks,
        _ => Empty
    };

    private static TraceTickSeries FromSource(TraceTimeline timeline, TraceTickSeries source, TraceTickDefinition definition)
    {
        var sources = new List<TraceTickSource>();

        foreach (var index in source.AllIndexes)
        {
            if (!definition.MatchesOperator(index.Operator))
            {
                continue;
            }

            for (var position = 0; position < index.Count; position++)
            {
                if (index.GetValue(position) == definition.Value
                    && (definition.Conditions.Count == 0 || definition.MatchesNode(index.Get(position).Node)))
                {
                    sources.Add(index.GetSource(position));
                }
            }
        }

        return Create(timeline, sources);
    }

    private static TraceTickSeries Combine(TraceTimeline timeline, IReadOnlyList<TraceTickSeries> series)
    {
        var seen = new HashSet<(uint Thread, int SpanIndex, TracePosition From)>();

        var sources = new List<TraceTickSource>();

        for (var value = 0; value < series.Count; value++)
        {
            foreach (var index in series[value].AllIndexes)
            {
                for (var position = 0; position < index.Count; position++)
                {
                    var source = index.GetSource(position);

                    if (seen.Add((source.Thread.ThreadId, source.SpanIndex, source.From.Position)))
                    {
                        sources.Add(source with { Value = (byte)value });
                    }
                }
            }
        }

        return Create(timeline, sources);
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
