using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

internal static class IteratorLifetimeBuilder
{
    public static TraceOperatorLifetimeList Build(TraceTimeline timeline,
                                               AllocationIndex allocation,
                                               IReadOnlyDictionary<ulong, ExecutionOperatorEvent> operators,
                                               IReadOnlyList<IteratorPlanNode> planNodes,
                                               IReadOnlyList<QueryTeardown> teardowns)
    {
        var owners = new Dictionary<ulong, ExecutionOperatorEvent>(operators);

        foreach (var (instance, adapter) in BatchToRowOperatorBuilder.Create(timeline, planNodes, operators))
        {
            owners.TryAdd(instance, adapter);
        }

        var lifetimes = new List<TraceOperatorLifetime>();

        if (owners.Count == 0)
        {
            return TraceOperatorLifetimeList.Empty;
        }

        var constructions = planNodes.Where(n => n.Constructed is not null).ToDictionary(n => n.Iterator, n => n.Constructed!.Value);

        var callsByThread = new Dictionary<uint, Dictionary<ExecutionOperatorEvent, OperatorCallList>>();

        foreach (var span in timeline.GetSpanLocations())
        {
            if (span.Node.Frame is not { InstanceAddress: not 0 } frame || !owners.TryGetValue(frame.InstanceAddress, out var owner))
            {
                continue;
            }

            if (!callsByThread.TryGetValue(span.Thread.ThreadId, out var calls))
            {
                calls = new Dictionary<ExecutionOperatorEvent, OperatorCallList>(ReferenceEqualityComparer.Instance);

                callsByThread[span.Thread.ThreadId] = calls;
            }

            if (!calls.TryGetValue(owner, out var operatorCalls))
            {
                operatorCalls = new OperatorCallList();

                calls[owner] = operatorCalls;
            }

            operatorCalls.Instances.Add(frame.InstanceAddress);

            operatorCalls.Calls.Add(span.Span);
        }

        foreach (var thread in timeline.Threads)
        {
            if (!callsByThread.TryGetValue(thread.ThreadId, out var calls))
            {
                continue;
            }

            var merged = calls.Select(c => (Operator: c.Key, c.Value.Instances, Calls: GetOutermost(c.Value.Calls))).ToList();

            var curves = allocation.InUseOwnedBy(thread.ThreadId, [.. merged.Select(m => m.Calls)]);

            TracePoint[] threadTeardowns = [.. teardowns.Where(t => t.Thread == thread.ThreadId)
                                                        .Select(t => t.End)
                                                        .OrderBy(e => e.Position)];

            for (var index = 0; index < merged.Count; index++)
            {
                var (owner, instances, spans) = merged[index];

                var constructed = instances.Where(constructions.ContainsKey).Select(i => constructions[i]).OrderBy(c => c.Position).ToList();

                var lastEnd = spans[^1].To;

                var destroyed = threadTeardowns.Where(t => t.Position >= lastEnd.Position).ToList();

                lifetimes.Add(new TraceOperatorLifetime(thread.ThreadId, owner, spans[0].CallNodeIndex, spans, curves[index], timeline.Scale)
                {
                    Constructed = constructed.Count > 0 ? GetEarliest(constructed[0], spans[0].From) : timeline.Start,
                    Destroyed = destroyed.Count > 0 ? destroyed[0] : timeline.End,
                    Instances = [.. instances]
                });
            }
        }

        return new TraceOperatorLifetimeList(lifetimes);
    }

    private static TraceSpan[] GetOutermost(List<TraceSpan> calls) => [.. Outermost.Of(calls, c => c.From.Position, c => c.To.Position)];

    private static TracePoint GetEarliest(TracePoint left, TracePoint right) => left.Position <= right.Position ? left : right;

    private sealed class OperatorCallList
    {
        public HashSet<ulong> Instances { get; } = [];

        public List<TraceSpan> Calls { get; } = [];
    }
}
