namespace InternalsViewer.Query.Plans;

public sealed class ExecutionPlan(short planHandleId)
{
    /// <summary>Interned id of this plan's handle (see <see cref="PlanHandleRegistry"/>).</summary>
    public short PlanHandleId { get; init; } = planHandleId;

    public List<PlanNode> Root { get; set; } = [];

    public Dictionary<int, PlanNode> NodesById { get; set; } = new();

    public bool IsInternalPlan { get; set; }
}
