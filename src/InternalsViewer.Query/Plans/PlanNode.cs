namespace InternalsViewer.Query.Plans;

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

    public long EstimatedRows { get; set; }

    public bool IsStatement { get; set; }

    public int NodeLevel { get; set; }

    public HashInfo? HashInfo { get; set; }

    public HashSet<string> Outputs { get; set; } = [];

    public long DurationUs { get; set; }

    public Dictionary<int, ThreadRuntime> CountersByThread { get; set; } = new();

    public long RowsProcessed => CountersByThread.Values.Sum(c => c.RowsProcessed);
}

public readonly record struct ThreadRuntime(long RowsProcessed, long ElapsedUs);