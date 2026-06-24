namespace InternalsViewer.Query.Plans;

public sealed class ExecutionPlan(string planHandle)
{
    public string PlanHandle { get; init; } = planHandle;

    public List<PlanNode> Root { get; set; } = [];

    public Dictionary<int, PlanNode> NodesById { get; set; } = new();

    public bool IsInternalPlan { get; set; }
}