using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class IteratorPlanLinker
{
    public static IteratorPlanLink Link(IReadOnlyList<IteratorPlanNode> planNodes,
                                        IReadOnlyList<CallStackNode?> nodes,
                                        IEnumerable<EngineEvent> events)
    {
        if (planNodes.Count == 0)
        {
            return IteratorPlanLink.Empty;
        }

        var plans = OperatorHierarchy.Build(events)
                                     .Operators
                                     .Where(o => o.PlanNodeIdentifier is { NodeId: >= 0 })
                                     .GroupBy(o => o.PlanNodeIdentifier!.PlanHandleId)
                                     .ToDictionary(g => g.Key, g => g.GroupBy(o => (int)o.PlanNodeIdentifier!.NodeId)
                                                                       .ToDictionary(n => n.Key, n => n.First()));

        var adapters = GetAdapters(planNodes);

        var operatorNames = GetOperatorNames(nodes);

        var linked = new HashSet<short>();

        var planObjects = new Dictionary<ulong, PlanNodeIdentifier>();

        var instances = new Dictionary<ulong, PlanNodeIdentifier>();

        foreach (var query in planNodes.Where(n => !adapters.Contains(n.Iterator)).GroupBy(n => n.Query))
        {
            if (GetPlan(query, plans, linked, operatorNames) is not var (planHandle, plan))
            {
                continue;
            }

            linked.Add(planHandle);

            foreach (var planNode in query)
            {
                var identifier = plan[planNode.NodeId].PlanNodeIdentifier!;

                instances.TryAdd(planNode.Iterator, identifier);

                planObjects.TryAdd(planNode.PlanObject, identifier);

                if (planNode.BatchPlanObject != 0)
                {
                    planObjects.TryAdd(planNode.BatchPlanObject, identifier);
                }
            }
        }

        return instances.Count == 0 ? IteratorPlanLink.Empty : new IteratorPlanLink(planObjects, instances);
    }

    private static HashSet<ulong> GetAdapters(IReadOnlyList<IteratorPlanNode> planNodes)
    {
        HashSet<ulong> batchPlanObjects = [.. planNodes.Where(n => n.BatchPlanObject != 0).Select(n => n.PlanObject)];

        return [.. planNodes.Where(n => n.BatchPlanObject == 0 && batchPlanObjects.Contains(n.PlanObject)).Select(n => n.Iterator)];
    }

    private static Dictionary<ulong, IReadOnlyList<GlobPattern>> GetOperatorNames(IReadOnlyList<CallStackNode?> nodes)
    {
        var names = new Dictionary<ulong, IReadOnlyList<GlobPattern>>();

        foreach (var node in nodes)
        {
            if (node?.Frame is { Instance: not 0 and var instance, Resolved.PlanOperator: { Count: > 0 } patterns })
            {
                names.TryAdd(instance, patterns);
            }
        }

        return names;
    }

    private static (short PlanHandle, Dictionary<int, ExecutionOperatorEvent> Plan)? GetPlan(
        IGrouping<ulong, IteratorPlanNode> query,
        Dictionary<short, Dictionary<int, ExecutionOperatorEvent>> plans,
        HashSet<short> linked,
        Dictionary<ulong, IReadOnlyList<GlobPattern>> operatorNames)
    {
        var candidates = plans.Where(p => !linked.Contains(p.Key) && query.All(n => p.Value.ContainsKey(n.NodeId)))
                              .Select(p => (Plan: p, Score: GetScore(query, p.Value, operatorNames)))
                              .Where(c => c.Score >= 0)
                              .OrderByDescending(c => c.Score)
                              .ThenBy(c => c.Plan.Key)
                              .ToList();

        return candidates.Count > 0 ? (candidates[0].Plan.Key, candidates[0].Plan.Value) : null;
    }

    private static int GetScore(IEnumerable<IteratorPlanNode> query,
                                Dictionary<int, ExecutionOperatorEvent> plan,
                                Dictionary<ulong, IReadOnlyList<GlobPattern>> operatorNames)
    {
        var score = 0;

        foreach (var planNode in query)
        {
            if (operatorNames.TryGetValue(planNode.Iterator, out var patterns))
            {
                score += patterns.Any(p => p.Matches(plan[planNode.NodeId].Name)) ? 1 : -1;
            }
        }

        return score;
    }
}
