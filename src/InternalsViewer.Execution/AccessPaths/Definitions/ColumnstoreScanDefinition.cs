using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record ColumnstoreScanDefinition : IteratorDefinition, IBatchDefinition
{
    public AllocationUnit? AllocationUnit { get; init; }

    public IReadOnlyList<string> ColumnNames { get; init; } = [];

    public bool IsFilterOnCompressedDataUsed { get; init; }
}
