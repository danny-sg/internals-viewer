using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class BatchToRowOperatorBuilder
{
    private const string OperatorName = "Batch To Row Adapter";

    public static Dictionary<ulong, ExecutionOperatorEvent> Create(TraceTimeline timeline,
                                                                   IReadOnlyList<IteratorPlanNode> planNodes,
                                                                   IReadOnlyDictionary<ulong, ExecutionOperatorEvent> owners)
    {
        var adapters = new Dictionary<ulong, ExecutionOperatorEvent>();

        if (planNodes.Count == 0)
        {
            return adapters;
        }

        var query = planNodes[0].Query;

        var operators = owners.Values.Distinct<ExecutionOperatorEvent>(ReferenceEqualityComparer.Instance).ToList();

        var planHandle = operators.FirstOrDefault()?.PlanNodeIdentifier?.PlanHandleId;

        foreach (var helpers in planNodes.Where(n => n.IsBatchHelper && n.Query == query).GroupBy(n => n.NodeId))
        {
            HashSet<ulong> instances = [.. helpers.Select(h => h.Iterator)];

            if ((GetAdaptedOperator(operators, planHandle, helpers.Key) ?? GetSubOperator(timeline, owners, instances)) is { } source)
            {
                var adapter = new ExecutionOperatorEvent
                {
                    Name = OperatorName,
                    OperatorDescription = OperatorName,
                    LogicalOperator = OperatorName,
                    Category = OperatorCategory.Adapter,
                    NodeLevel = source.NodeLevel,
                    IsBlockingInput = source.IsBlockingInput,
                    AdaptedNode = source.PlanNodeIdentifier
                };

                foreach (var instance in instances)
                {
                    adapters.TryAdd(instance, adapter);
                }
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
                                                              IReadOnlyDictionary<ulong, ExecutionOperatorEvent> owners,
                                                              HashSet<ulong> helpers)
    {
        var seen = new HashSet<CallTreeNode>(ReferenceEqualityComparer.Instance);

        var pending = new Queue<CallTreeNode>();

        for (var callNodeIndex = 0; callNodeIndex < timeline.NodeCount; callNodeIndex++)
        {
            if (timeline.NodeOf(callNodeIndex) is { Frame.InstanceAddress: var instance } callNode
                && helpers.Contains(instance)
                && seen.Add(callNode))
            {
                pending.Enqueue(callNode);
            }
        }

        while (pending.TryDequeue(out var callNode))
        {
            foreach (var child in callNode.ChildNodes)
            {
                if (child.Frame is { InstanceAddress: not 0 } frame && owners.TryGetValue(frame.InstanceAddress, out var owner))
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
