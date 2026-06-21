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

    /// <summary>
    /// True for the synthetic statement node (the SSMS SELECT/INSERT/... root) that sits to the
    /// left of the top relational operator. Its <see cref="PhysicalOperator"/> holds the statement
    /// type and its <see cref="Children"/> are the query plan's root relational operators.
    /// </summary>
    public bool IsStatement { get; set; }
}

public record PlanNodeIdentifier
{
    public string PlanHandle { get; set; }

    public int NodeId { get; set; }
}