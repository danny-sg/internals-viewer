namespace InternalsViewer.Replay.Plans;

public sealed class ExecutionPlan
{
    public List<PlanNode> Roots { get; set; } = new();

    public Dictionary<int, PlanNode> NodesById { get; set; } = new();
}