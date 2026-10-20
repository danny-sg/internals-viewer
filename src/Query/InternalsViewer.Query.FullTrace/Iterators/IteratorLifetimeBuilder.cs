using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class IteratorLifetimeBuilder
{
    public static IReadOnlyList<TraceOperatorLifetime> Build(TraceTimeline timeline,
                                                             IReadOnlyList<ExecutionOperatorEvent> operators,
                                                             IReadOnlyList<IteratorPlanNode> planNodes,
                                                             IReadOnlyList<QueryTeardown> teardowns)
    {
        var owners = new Dictionary<ulong, ExecutionOperatorEvent>();

        foreach (var operatorEvent in operators.Concat(BatchToRowOperators.Create(timeline, planNodes, operators)))
        {
            foreach (var instance in operatorEvent.Instances)
            {
                owners.TryAdd(instance, operatorEvent);
            }
        }

        var lifetimes = new List<TraceOperatorLifetime>();

        if (owners.Count == 0)
        {
            return lifetimes;
        }

        var constructions = planNodes.Where(n => n.Constructed is not null).ToDictionary(n => n.Iterator, n => n.Constructed!.Value);

        var callsByThread = new Dictionary<uint, Dictionary<ExecutionOperatorEvent, OperatorCalls>>();

        foreach (var span in timeline.GetSpanLocations())
        {
            if (span.Node.Frame is not { Instance: not 0 } frame || !owners.TryGetValue(frame.Instance, out var owner))
            {
                continue;
            }

            if (!callsByThread.TryGetValue(span.Thread.ThreadId, out var calls))
            {
                calls = new Dictionary<ExecutionOperatorEvent, OperatorCalls>(ReferenceEqualityComparer.Instance);

                callsByThread[span.Thread.ThreadId] = calls;
            }

            if (!calls.TryGetValue(owner, out var operatorCalls))
            {
                operatorCalls = new OperatorCalls();

                calls[owner] = operatorCalls;
            }

            operatorCalls.Instances.Add(frame.Instance);

            operatorCalls.Calls.Add(new InstanceCall(span.Span(TraceTimelineAxis.Position), span.Span(TraceTimelineAxis.Instructions)));
        }

        foreach (var thread in timeline.Threads)
        {
            if (!callsByThread.TryGetValue(thread.ThreadId, out var calls))
            {
                continue;
            }

            var merged = calls.Select(c => (Operator: c.Key, c.Value.Instances, Calls: Merged(c.Value.Calls))).ToList();

            var curves = timeline.Memory.InUseOwnedBy(thread.ThreadId, [.. merged.Select(m => m.Calls.Positions)]);

            TracePoint[] threadTeardowns = [.. teardowns.Where(t => t.Thread == thread.ThreadId)
                                                        .Select(t => t.End)
                                                        .OrderBy(e => e.Position)];

            for (var index = 0; index < merged.Count; index++)
            {
                var (owner, instances, (positions, instructions)) = merged[index];

                var constructed = instances.Where(constructions.ContainsKey).Select(i => constructions[i]).OrderBy(c => c.Position).ToList();

                var lastEnd = positions[^1].End;

                var destroyed = threadTeardowns.Where(t => timeline.PositionOf(t.Position) >= lastEnd).ToList();

                lifetimes.Add(new TraceOperatorLifetime(thread.ThreadId,
                                                        owner,
                                                        positions[0].Node,
                                                        positions,
                                                        instructions,
                                                        curves[index])
                {
                    ConstructedPosition = constructed.Count > 0
                        ? Math.Min(timeline.PositionOf(constructed[0].Position), positions[0].Start)
                        : timeline.StartOf(TraceTimelineAxis.Position),
                    ConstructedInstructions = constructed.Count > 0
                        ? Math.Min(constructed[0].Instructions, instructions[0].Start)
                        : timeline.StartOf(TraceTimelineAxis.Instructions),
                    DestroyedPosition = destroyed.Count > 0
                        ? timeline.PositionOf(destroyed[0].Position)
                        : timeline.EndOf(TraceTimelineAxis.Position),
                    DestroyedInstructions = destroyed.Count > 0
                        ? Math.Max(destroyed[0].Instructions, instructions[^1].End)
                        : timeline.EndOf(TraceTimelineAxis.Instructions)
                });
            }
        }

        return lifetimes;
    }

    private static (TraceSpan[] Positions, TraceSpan[] Instructions) Merged(List<InstanceCall> calls)
    {
        var outermost = Outermost.Of(calls, c => c.Position.Start, c => c.Position.End);

        return ([.. outermost.Select(c => c.Position)], [.. outermost.Select(c => c.Instructions)]);
    }

    private readonly record struct InstanceCall(TraceSpan Position, TraceSpan Instructions);

    private sealed class OperatorCalls
    {
        public HashSet<ulong> Instances { get; } = [];

        public List<InstanceCall> Calls { get; } = [];
    }
}
