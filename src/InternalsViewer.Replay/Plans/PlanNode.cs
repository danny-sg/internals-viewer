namespace InternalsViewer.Replay.Plans;

public sealed class PlanNode
{
    public int NodeId { get; set; }

    public string PhysicalOperator { get; set; } = string.Empty;

    public string LogicalOperator { get; set; } = string.Empty;

    public List<PlanNode> Children { get; set; } = [];

    public string? Schema { get; set; }

    public string? Table { get; set; }

    public string? Index { get; set; }

    public double? EstimatedCost { get; set; }
}

public record PlanNodeIdentifier
{
    public string PlanHandle { get; set; }

    public int NodeId { get; set; }
}