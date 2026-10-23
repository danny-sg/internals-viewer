using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class IteratorPlanLinker
{
    public static IteratorPlanLink Link(IReadOnlyList<IteratorPlanNode> planNodes, IEnumerable<EngineEvent> events)
    {
        var operators = OperatorHierarchy.Build(events).Operators;

        if (planNodes.Count == 0 || operators.Count == 0)
        {
            return IteratorPlanLink.Empty;
        }

        var planHandle = operators.OrderBy(o => o.PlanNodeIdentifier!.NodeId >= 0).First().PlanNodeIdentifier!.PlanHandleId;

        var plan = operators.Where(o => o.PlanNodeIdentifier is { NodeId: >= 0 } node && node.PlanHandleId == planHandle)
                            .Select(o => o.PlanNodeIdentifier!)
                            .DistinctBy(n => n.NodeId)
                            .ToDictionary(n => (int)n.NodeId);

        var query = planNodes[0].Query;

        var planObjects = new Dictionary<ulong, PlanNodeIdentifier>();

        var instances = new Dictionary<ulong, PlanNodeIdentifier>();

        foreach (var planNode in planNodes.Where(n => n.Query == query && !n.IsBatchHelper))
        {
            if (!plan.TryGetValue(planNode.NodeId, out var identifier))
            {
                continue;
            }

            instances.TryAdd(planNode.Iterator, identifier);

            planObjects.TryAdd(planNode.PlanObject, identifier);

            if (planNode.BatchPlanObject != 0)
            {
                planObjects.TryAdd(planNode.BatchPlanObject, identifier);
            }
        }

        return instances.Count == 0 ? IteratorPlanLink.Empty : new IteratorPlanLink(planObjects, instances);
    }
}
