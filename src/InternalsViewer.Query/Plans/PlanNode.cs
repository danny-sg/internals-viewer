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

    public Dictionary<int, ThreadRuntime> CountersByThread { get; set; } = new();

    /// <summary>Total rows processed across all threads (run-time).</summary>
    public long RowsProcessed => CountersByThread.Values.Sum(c => c.RowsProcessed);

    /// <summary>
    /// The operator's wall-clock duration: the coordinator thread's (thread 0) elapsed time, which
    /// spans the whole parallel region; falls back to the longest worker, and to 0 when the plan has no
    /// run-time information (an estimated plan). Sourced from the plan's per-thread counters, so it is
    /// ready after parse.
    /// </summary>
    public long DurationUs =>
        CountersByThread.TryGetValue(0, out var coordinator) && coordinator.ElapsedUs > 0
            ? coordinator.ElapsedUs
            : CountersByThread.Values.Select(c => c.ElapsedUs).DefaultIfEmpty(0).Max();
}

public readonly record struct ThreadRuntime(long RowsProcessed, long ElapsedUs);