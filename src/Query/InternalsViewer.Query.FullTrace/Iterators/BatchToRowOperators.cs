using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class BatchToRowOperators
{
    private const string OperatorName = "Batch To Row Adapter";

    public static List<ExecutionOperatorEvent> Create(TraceTimeline timeline,
                                                      IReadOnlyList<IteratorPlanNode> planNodes,
                                                      IReadOnlyList<ExecutionOperatorEvent> operators)
    {
        var adapters = new List<ExecutionOperatorEvent>();

        if (planNodes.Count == 0)
        {
            return adapters;
        }

        var query = planNodes[0].Query;

        var planHandle = operators.FirstOrDefault(o => o.Instances.Count > 0)?.PlanNodeIdentifier?.PlanHandleId;

        foreach (var helpers in planNodes.Where(n => n.IsBatchHelper && n.Query == query).GroupBy(n => n.NodeId))
        {
            HashSet<ulong> instances = [.. helpers.Select(h => h.Iterator)];

            if ((GetAdaptedOperator(operators, planHandle, helpers.Key) ?? GetSubOperator(timeline, operators, instances)) is { } source)
            {
                adapters.Add(new ExecutionOperatorEvent
                {
                    Name = OperatorName,
                    OperatorDescription = OperatorName,
                    LogicalOperator = OperatorName,
                    Category = OperatorCategory.Adapter,
                    NodeLevel = source.NodeLevel,
                    IsBlockingInput = source.IsBlockingInput,
                    Instances = [.. instances]
                });
            }
        }

        return adapters;
    }

    private static ExecutionOperatorEvent? GetAdaptedOperator(IReadOnlyList<ExecutionOperatorEvent> operators,
                                                              short? planHandle,
                                                              int nodeId)
        => operators.FirstOrDefault(o => o.PlanNodeIdentifier is { } node
                                         && node.NodeId == nodeId
                                         && (planHandle is null || node.PlanHandleId == planHandle));

    private static ExecutionOperatorEvent? GetSubOperator(TraceTimeline timeline,
                                                              IReadOnlyList<ExecutionOperatorEvent> operators,
                                                              HashSet<ulong> helpers)
    {
        var owners = new Dictionary<ulong, ExecutionOperatorEvent>();

        foreach (var operatorEvent in operators)
        {
            foreach (var instance in operatorEvent.Instances)
            {
                owners.TryAdd(instance, operatorEvent);
            }
        }

        var seen = new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance);

        var pending = new Queue<CallStackNode>();

        for (var node = 0; node < timeline.NodeCount; node++)
        {
            if (timeline.NodeOf(node) is { Frame.Instance: var instance } callNode && helpers.Contains(instance) && seen.Add(callNode))
            {
                pending.Enqueue(callNode);
            }
        }

        while (pending.TryDequeue(out var callNode))
        {
            foreach (var child in callNode.ChildNodes)
            {
                if (child.Frame is { Instance: not 0 } frame && owners.TryGetValue(frame.Instance, out var owner))
                {
                    return owner;
                }

                if (seen.Add(child))
                {
                    pending.Enqueue(child);
                }
            }
        }

        return null;
    }
}
