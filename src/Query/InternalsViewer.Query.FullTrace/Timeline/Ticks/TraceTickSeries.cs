using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Plans.Operators;

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

    public IEnumerable<TraceTick> GetRunningTicks(TracePosition position) => AllIndexes.SelectMany(i => i.GetRunningTicks(position));

    public IEnumerable<TraceTick> GetEndedTicks(TracePosition after, TracePosition upTo)
        => AllIndexes.SelectMany(i => i.GetEndedTicks(after, upTo));

    public IEnumerable<TraceTick> GetTicks(Func<byte, bool> include)
    {
        foreach (var index in AllIndexes)
        {
            for (var position = 0; position < index.Count; position++)
            {
                if (include(index.GetValue(position)))
                {
                    yield return index.Get(position);
                }
            }
        }
    }

    public TraceTick? GetNext(TracePosition after, Func<byte, bool> include)
    {
        TraceTick? next = null;

        foreach (var index in AllIndexes)
        {
            if (index.GetNext(after, include) is { } tick
                && (next is not { } current || tick.Span.From.Position < current.Span.From.Position))
            {
                next = tick;
            }
        }

        return next;
    }

    public TraceTick? GetPrevious(TracePosition before, Func<byte, bool> include)
    {
        TraceTick? previous = null;

        foreach (var index in AllIndexes)
        {
            if (index.GetPrevious(before, include) is { } tick
                && (previous is not { } current || tick.Span.From.Position > current.Span.From.Position))
            {
                previous = tick;
            }
        }

        return previous;
    }

    public IEnumerable<TraceTick> GetLatestTicks(TracePosition position)
    {
        foreach (var threads in Indexes.Values)
        {
            TraceTick? latest = null;

            foreach (var index in threads.Values)
            {
                if (index.GetLatest(position) is { } tick
                    && (latest is not { } current || tick.Span.From.Position > current.Span.From.Position))
                {
                    latest = tick;
                }
            }

            if (latest is { } found)
            {
                yield return found;
            }
        }
    }

    public IEnumerable<(ExecutionOperatorEvent Operator, int Count)> CountEnded(IReadOnlyCollection<byte> values, TracePosition upTo)
    {
        foreach (var (operatorEvent, threads) in Indexes)
        {
            var count = threads.Values.Sum(i => values.Sum(v => i.CountEnded(v, upTo)));

            if (count > 0)
            {
                yield return (operatorEvent, count);
            }
        }
    }

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

    public static TraceTickSeries Build(TraceResult trace, TraceTickDefinition definition) => definition.Source switch
    {
        TraceTickSourceType.Call => Build(trace.Timeline, trace.CallStore, trace.Lifetimes, definition, trace.EnclosingCalls),
        TraceTickSourceType.Watch => TraceWatch.Build(trace, definition).Series,
        _ => Filter(trace.Timeline, GetSource(trace, definition.Source), definition)
    };

    public static TraceTickSeries BuildAssigned(TraceResult trace,
                                                TraceTickDefinition definition,
                                                ExecutionOperatorEvent? statement,
                                                Func<ExecutionOperatorEvent, IReadOnlyList<ExecutionOperatorEvent>> getOperators)
    {
        var owners = trace.Lifetimes.OperatorsByInstance;

        var nodes = new Dictionary<CallTreeNode, bool>(ReferenceEqualityComparer.Instance);

        var evaluations = new List<(TraceTickSource Source, ExecutionOperatorEvent? Owner)>();

        foreach (var list in trace.CallStore.GetCallLists())
        {
            if (trace.CallStore.GetFirstNode(list) is not { Frame: not null } first || !definition.IsFunction(first))
            {
                continue;
            }

            var owner = owners.GetValueOrDefault(list.InstanceAddress);

            for (var index = 0; index < list.Count; index++)
            {
                if (trace.Timeline.FindCall(list.FunctionAddress, list.InstanceAddress, index) is not { } location)
                {
                    continue;
                }

                if (!nodes.TryGetValue(location.Node, out var isMatch))
                {
                    isMatch = definition.MatchesNode(location.Node);

                    nodes[location.Node] = isMatch;
                }

                if (isMatch)
                {
                    evaluations.Add((TraceTickSource.Create(location, 0), owner));
                }
            }
        }

        var points = evaluations.Select(e => e.Source)
                                .Select(s => new SweepSpan<TracePosition>(s.Thread.ThreadId, s.From.Position, s.To.Position, 0))
                                .ToList();

        var pullers = trace.PullerCalls.GetEnclosing(points, includeRowReturns: true);

        var assigned = new Dictionary<ExecutionOperatorEvent, IReadOnlyList<ExecutionOperatorEvent>>(ReferenceEqualityComparer.Instance);

        var sources = new List<TraceTickSource>();

        for (var index = 0; index < evaluations.Count; index++)
        {
            var (source, owner) = evaluations[index];

            var puller = owners.GetValueOrDefault(pullers[index]) ?? statement;

            if (owner is not null && OperatorClassifier.IsComputeScalar(owner))
            {
                sources.Add(source with { Operator = owner, Caller = puller });
            }
            else if (puller is not null)
            {
                if (!assigned.TryGetValue(puller, out var operators))
                {
                    operators = getOperators(puller);

                    assigned[puller] = operators;
                }

                sources.AddRange(operators.Select(o => source with { Operator = o, Caller = puller }));
            }
        }

        return Create(trace.Timeline, sources);
    }

    public static TraceTickTrackSeries BuildTrack(TraceResult trace,
                                                  IReadOnlyList<TraceTickDefinition> rules,
                                                  Func<TraceTickDefinition, TraceTickSeries> getSeries)
    {
        if (IsWholeSource(rules))
        {
            return new TraceTickTrackSeries(GetSource(trace, rules[0].Source), [.. rules.Select(r => r.Value)]);
        }

        if (rules.Count == 1)
        {
            var value = rules[0].Source is TraceTickSourceType.Call or TraceTickSourceType.Evaluate ? (byte)0 : rules[0].Value;

            return new TraceTickTrackSeries(getSeries(rules[0]), [value]);
        }

        var count = Math.Min(rules.Count, byte.MaxValue + 1);

        return new TraceTickTrackSeries(Combine(trace.Timeline, [.. rules.Take(count).Select(getSeries)]),
                                        [.. Enumerable.Range(0, count).Select(i => (byte)i)]);
    }

    internal static TraceTickSeries Build(TraceTimeline timeline,
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
                     (_, call) => definition.MatchesCall(call) ? 0 : null,
                     node => nodes.TryGetValue(node, out var isMatch) ? isMatch : nodes[node] = definition.MatchesNode(node),
                     definition.MatchesOperator);
    }

    internal static TraceTickSeries Build(TraceTimeline timeline,
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
           && rules.All(r => r.Source is not (TraceTickSourceType.Call
                                              or TraceTickSourceType.Watch
                                              or TraceTickSourceType.Evaluate
                                              or TraceTickSourceType.Statement)
                             && r.Source == rules[0].Source
                             && r.Conditions.Count == 0)
           && rules.Select(r => r.Value).Distinct().Count() == rules.Count;

    private static TraceTickSeries GetSource(TraceResult trace, TraceTickSourceType source) => source switch
    {
        TraceTickSourceType.RowReturn => trace.RowReturnMarks.Ticks,
        TraceTickSourceType.RowAccess => trace.RowAccesses,
        TraceTickSourceType.PageHeaderAccess => trace.PageHeaderAccesses,
        TraceTickSourceType.Seek => trace.Seeks,
        TraceTickSourceType.PageFetch => trace.PageFetches,
        TraceTickSourceType.Open => trace.Opens,
        _ => Empty
    };

    private static TraceTickSeries Filter(TraceTimeline timeline, TraceTickSeries source, TraceTickDefinition definition)
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
                    && (definition.Conditions.Count == 0 || definition.MatchesWithin(index.Get(position).Node)))
                {
                    sources.Add(index.GetSource(position));
                }
            }
        }

        return Create(timeline, sources);
    }

    private static TraceTickSeries Combine(TraceTimeline timeline, IReadOnlyList<TraceTickSeries> series)
    {
        var seen = new HashSet<(uint Thread, int SpanIndex, TracePosition From, int? NodeId)>();

        var sources = new List<TraceTickSource>();

        for (var value = 0; value < series.Count; value++)
        {
            foreach (var index in series[value].AllIndexes)
            {
                for (var position = 0; position < index.Count; position++)
                {
                    var source = index.GetSource(position);

                    var nodeId = source.Operator?.PlanNodeIdentifier?.NodeId;

                    if (seen.Add((source.Thread.ThreadId, source.SpanIndex, source.From.Position, nodeId)))
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

                sources.Add(TraceTickSource.Create(location, value));
            }
        }

        return Build(timeline, sources, lifetimes, enclosing, isOperator);
    }
}
