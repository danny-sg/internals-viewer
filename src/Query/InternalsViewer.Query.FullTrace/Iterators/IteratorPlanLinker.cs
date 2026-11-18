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

        var planHandle = GetPlanHandle(operators);

        var plan = operators.Where(o => o.PlanNodeIdentifier is { NodeId: >= 0 } node && node.PlanHandleId == planHandle)
                            .Select(o => o.PlanNodeIdentifier!)
                            .DistinctBy(n => n.NodeId)
                            .ToDictionary(n => (int)n.NodeId);

        var query = GetQuery(planNodes);

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

    internal static short GetPlanHandle(IReadOnlyList<ExecutionOperatorEvent> operators)
    {
        var plans = operators.Where(o => o.PlanNodeIdentifier is not null).GroupBy(o => o.PlanNodeIdentifier!.PlanHandleId).ToList();

        var statements = plans.Where(p => p.Any(o => o.PlanNodeIdentifier!.NodeId < 0)).ToList();

        return (statements.Count > 0 ? statements : plans).MaxBy(p => p.Min(o => o.TimeUs))!.Key;
    }

    internal static ulong GetQuery(IReadOnlyList<IteratorPlanNode> planNodes)
        => planNodes.Where(n => n.Constructed is not null)
                    .GroupBy(n => n.Query)
                    .MaxBy(g => g.Min(n => n.Constructed!.Value.Position))?.Key
           ?? planNodes[0].Query;
}
