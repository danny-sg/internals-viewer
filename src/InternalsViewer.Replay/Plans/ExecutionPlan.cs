namespace InternalsViewer.Replay.Plans;

public sealed class ExecutionPlan(string planHandle)
{
    public string PlanHandle { get; init; } = planHandle;

    public List<PlanNode> Roots { get; set; } = new();

    public Dictionary<int, PlanNode> NodesById { get; set; } = new();
}