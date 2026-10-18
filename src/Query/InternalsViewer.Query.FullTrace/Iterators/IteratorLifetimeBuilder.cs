using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class IteratorLifetimeBuilder
{
    public static IReadOnlyList<TraceOperatorLifetime> Build(TraceTimeline timeline,
                                                             IReadOnlyList<ExecutionOperatorEvent> operators,
                                                             BatchStore batches,
                                                             IteratorTree tree)
    {
        var owners = new Dictionary<ulong, ExecutionOperatorEvent>();

        foreach (var operatorEvent in operators.Concat(BatchToRowOperators.Create(timeline, batches, operators)))
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

        var depths = new Dictionary<ExecutionOperatorEvent, int>(ReferenceEqualityComparer.Instance);

        var callsByThread = new Dictionary<uint, Dictionary<ExecutionOperatorEvent, List<InstanceCall>>>();

        foreach (var span in timeline.GetSpanLocations())
        {
            if (span.Node.Frame is not { Instance: not 0 } frame || !owners.TryGetValue(frame.Instance, out var owner))
            {
                continue;
            }

            if (!callsByThread.TryGetValue(span.Thread.ThreadId, out var calls))
            {
                calls = new Dictionary<ExecutionOperatorEvent, List<InstanceCall>>(ReferenceEqualityComparer.Instance);

                callsByThread[span.Thread.ThreadId] = calls;
            }

            if (!calls.TryGetValue(owner, out var spans))
            {
                spans = [];

                calls[owner] = spans;
            }

            spans.Add(new InstanceCall(span.Span(TraceTimelineAxis.Position), span.Span(TraceTimelineAxis.Instructions)));
        }

        foreach (var thread in timeline.Threads)
        {
            if (!callsByThread.TryGetValue(thread.ThreadId, out var calls))
            {
                continue;
            }

            var merged = calls.Select(c => (Operator: c.Key, Calls: Merged(c.Value))).ToList();

            var curves = timeline.Memory.InUseOwnedBy(thread.ThreadId, [.. merged.Select(m => m.Calls.Positions)]);

            for (var index = 0; index < merged.Count; index++)
            {
                var (owner, (positions, instructions)) = merged[index];

                lifetimes.Add(new TraceOperatorLifetime(thread.ThreadId,
                                                        owner,
                                                        positions[0].Node,
                                                        positions,
                                                        instructions,
                                                        curves[index])
                {
                    Depth = GetDepth(owner, owners, tree, depths)
                });
            }
        }

        return lifetimes;
    }

    private static int GetDepth(ExecutionOperatorEvent operatorEvent,
                                Dictionary<ulong, ExecutionOperatorEvent> owners,
                                IteratorTree tree,
                                Dictionary<ExecutionOperatorEvent, int> depths)
    {
        if (depths.TryGetValue(operatorEvent, out var known))
        {
            return known;
        }

        depths[operatorEvent] = operatorEvent.NodeLevel;

        if (!operatorEvent.Instances.Any(tree.Contains))
        {
            return operatorEvent.NodeLevel;
        }

        var depth = GetParentOperator(operatorEvent, owners, tree) is { } parent ? GetDepth(parent, owners, tree, depths) + 1 : 0;

        depths[operatorEvent] = depth;

        return depth;
    }

    private static ExecutionOperatorEvent? GetParentOperator(ExecutionOperatorEvent operatorEvent,
                                                             Dictionary<ulong, ExecutionOperatorEvent> owners,
                                                             IteratorTree tree)
    {
        foreach (var instance in operatorEvent.Instances)
        {
            for (var parent = tree.GetParent(instance); parent != 0; parent = tree.GetParent(parent))
            {
                if (owners.TryGetValue(parent, out var owner) && !ReferenceEquals(owner, operatorEvent))
                {
                    return owner;
                }
            }
        }

        return null;
    }

    private static (TraceSpan[] Positions, TraceSpan[] Instructions) Merged(List<InstanceCall> calls)
    {
        var outermost = Outermost.Of(calls, c => c.Position.Start, c => c.Position.End);

        return ([.. outermost.Select(c => c.Position)], [.. outermost.Select(c => c.Instructions)]);
    }

    private readonly record struct InstanceCall(TraceSpan Position, TraceSpan Instructions);
}
