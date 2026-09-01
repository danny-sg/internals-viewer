using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record ColumnstoreScanDefinition : IteratorDefinition, IBatchDefinition
{
    public AllocationUnit? AllocationUnit { get; init; }

    public IReadOnlyList<string> ColumnNames { get; init; } = [];

    /// <summary>
    /// Columns added to the batch by operators above the scan
    /// </summary>
    public IReadOnlyList<string> PipelineColumnNames { get; init; } = [];

    public bool IsFilterOnCompressedDataUsed { get; init; }

    public bool IsGenericFilterUsed { get; init; }

    public bool IsAggregatePushdown { get; init; }

    public int BatchRowCount => BatchMode.BatchSize.GetRowCount(ColumnNames.Count + PipelineColumnNames.Count);
}
