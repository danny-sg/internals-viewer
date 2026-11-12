using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal readonly partial record struct IteratorPlanNode(ulong Iterator,
                                                         ulong PlanObject,
                                                         ulong BatchPlanObject,
                                                         ulong Query,
                                                         int NodeId,
                                                         TracePoint? Constructed = null,
                                                         bool IsBatchHelper = false)
{
    public IReadOnlyList<PlanOutputColumn> Outputs { get; init; } = [];

    internal static Dictionary<int, IReadOnlyList<PlanOutputColumn>> GetOutputColumns(IReadOnlyList<IteratorPlanNode> planNodes)
    {
        var nodeIds = new Dictionary<ulong, int>();

        foreach (var planNode in planNodes)
        {
            nodeIds.TryAdd(planNode.PlanObject, planNode.NodeId);
        }

        var columns = new Dictionary<int, IReadOnlyList<PlanOutputColumn>>();

        foreach (var planNode in planNodes)
        {
            if (planNode.Outputs.Count > 0 && !columns.ContainsKey(planNode.NodeId))
            {
                columns[planNode.NodeId] = [.. planNode.Outputs.Select(c => c with
                {
                    ProducerNodeId = nodeIds.GetValueOrDefault(c.Producer, -1)
                })];
            }
        }

        return columns;
    }
}
