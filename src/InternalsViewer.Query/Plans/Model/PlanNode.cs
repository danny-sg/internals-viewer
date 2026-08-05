namespace InternalsViewer.Query.Plans.Model;

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

    public ScanInfo? ScanInfo { get; set; }

    public PredicateInfo? PredicateInfo { get; set; }

    public List<ColumnReference> OutputColumns { get; set; } = [];

    public List<DefinedValueInfo> DefinedValues { get; set; } = [];

    public List<SortColumnInfo> SortColumns { get; set; } = [];

    public SortInfo? SortInfo { get; set; }

    public MergeInfo? MergeInfo { get; set; }

    public TopInfo? TopInfo { get; set; }

    public NestedLoopsInfo? NestedLoopsInfo { get; set; }

    public List<ColumnReference> GroupByColumns { get; set; } = [];

    public PlanIoStatistics? IoStats { get; set; }

    public PlanMemoryGrant? MemoryGrant { get; set; }

    public QueryMemoryGrant? QueryMemoryGrant { get; set; }

    public long RowsOutput { get; set; }

    public long? RowsRead { get; set; }

    public HashSet<string> Outputs { get; set; } = [];

    public Dictionary<int, ThreadRuntime> CountersByThread { get; set; } = new();

    public long RowsProcessed => CountersByThread.Values.Sum(c => c.RowsProcessed);

    /// <summary>
    /// The operator's wall-clock duration: the coordinator thread's (thread 0) elapsed time, which
    /// spans the whole parallel region; falls back to the longest worker, and to 0 when the plan has no
    /// run-time information (an estimated plan). Sourced from the plan's per-thread counters, so it is
    /// ready after parse.
    /// </summary>
    public long DurationUs =>
        CountersByThread.TryGetValue(0, out var coordinator) 
        && coordinator.ElapsedUs > 0
            ? coordinator.ElapsedUs
            : CountersByThread.Values.Select(c => c.ElapsedUs).DefaultIfEmpty(0).Max();
}