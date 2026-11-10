using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

public static class TraceTrackBuilder
{
    public static TraceTickTrackSeries Build(TraceResult trace,
                                             IReadOnlyList<TraceTrackRule> rules,
                                             ExecutionOperatorEvent? statement,
                                             Func<ExecutionOperatorEvent, ExpressionPullSite, List<ExecutionOperatorEvent>> getPulled)
    {
        var watches = new Dictionary<TraceTrackRule, TraceWatch>(ReferenceEqualityComparer.Instance);

        var track = BuildTrack(trace, rules, rule => rule.Source switch
        {
            TraceTickSourceType.Watch
                => GetWatch(trace, rule, watches).Series,
            TraceTickSourceType.Evaluate
                => BuildAssigned(trace, rule, statement, p => getPulled(p, (ExpressionPullSite)rule.Value)),
            TraceTickSourceType.Statement
                => BuildAssigned(trace, rule, statement, _ => statement is null ? [] : [statement]),
            _ => Build(trace, rule)
        });

        return track with { Watches = watches };
    }

    internal static TraceTickSeries Build(TraceResult trace, TraceTrackRule rule)
        => rule.Source == TraceTickSourceType.Call
            ? Build(trace.Timeline, trace.CallStore, trace.Lifetimes, rule, trace.SourceTracks.EnclosingCalls)
            : Filter(trace.Timeline, trace.SourceTracks.GetSeries(rule.Source), rule);

    internal static TraceTickSeries BuildAssigned(TraceResult trace,
                                                  TraceTrackRule rule,
                                                  ExecutionOperatorEvent? statement,
                                                  Func<ExecutionOperatorEvent, IReadOnlyList<ExecutionOperatorEvent>> getOperators)
    {
        var owners = trace.Lifetimes.OperatorsByInstance;

        var nodes = new Dictionary<CallTreeNode, bool>(ReferenceEqualityComparer.Instance);

        var evaluations = new List<(TraceTickSource Source, ExecutionOperatorEvent? Owner)>();

        foreach (var list in trace.CallStore.GetCallLists())
        {
            if (trace.CallStore.GetFirstNode(list) is not { Frame: not null } first || !rule.IsFunction(first))
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
                    isMatch = rule.MatchesNode(location.Node);

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

        var pullers = trace.SourceTracks.PullerCalls.GetEnclosing(points, includeRowReturns: true);

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

        return TraceTickSeries.Create(trace.Timeline, sources);
    }

    internal static TraceTickTrackSeries BuildTrack(TraceResult trace,
                                                    IReadOnlyList<TraceTrackRule> rules,
                                                    Func<TraceTrackRule, TraceTickSeries> getSeries)
    {
        if (IsWholeSource(rules))
        {
            return new TraceTickTrackSeries(trace.SourceTracks.GetSeries(rules[0].Source), [.. rules.Select(r => r.Value)]);
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
                                          TraceTrackRule rule,
                                          EnclosingOperatorCallIndex enclosing)
    {
        var nodes = new Dictionary<CallTreeNode, bool>(ReferenceEqualityComparer.Instance);

        return Build(timeline,
                     calls,
                     lifetimes,
                     enclosing,
                     rule.IsFunction,
                     (_, call) => rule.MatchesCall(call) ? 0 : null,
                     node => nodes.TryGetValue(node, out var isMatch) ? isMatch : nodes[node] = rule.MatchesNode(node),
                     rule.MatchesOperator);
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

        return TraceTickSeries.Create(timeline, owned);
    }

    private static TraceWatch GetWatch(TraceResult trace, TraceTrackRule rule, Dictionary<TraceTrackRule, TraceWatch> watches)
    {
        if (!watches.TryGetValue(rule, out var watch))
        {
            watch = TraceWatch.Build(trace, rule);

            watches[rule] = watch;
        }

        return watch;
    }

    private static bool IsWholeSource(IReadOnlyList<TraceTrackRule> rules)
        => rules.Count > 0
           && rules.All(r => r.Source is not (TraceTickSourceType.Call
                                              or TraceTickSourceType.Watch
                                              or TraceTickSourceType.Evaluate
                                              or TraceTickSourceType.Statement)
                             && r.Source == rules[0].Source
                             && r.Conditions.Count == 0)
           && rules.Select(r => r.Value).Distinct().Count() == rules.Count;

    private static TraceTickSeries Filter(TraceTimeline timeline, TraceTickSeries source, TraceTrackRule rule)
    {
        var sources = new List<TraceTickSource>();

        foreach (var index in source.AllIndexes)
        {
            if (!rule.MatchesOperator(index.Operator))
            {
                continue;
            }

            for (var position = 0; position < index.Count; position++)
            {
                if (index.GetValue(position) == rule.Value
                    && (rule.Conditions.Count == 0 || rule.MatchesWithin(index.Get(position).Node)))
                {
                    sources.Add(index.GetSource(position));
                }
            }
        }

        return TraceTickSeries.Create(timeline, sources);
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

        return TraceTickSeries.Create(timeline, sources);
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
