using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes a scan over the compressed row groups of a columnstore index
/// </summary>
public sealed record ColumnstoreScanDefinition : IteratorDefinition
{
    public IReadOnlyList<RowGroup> RowGroups { get; init; } = [];

    /// <summary>
    /// The columnstore column ids to project, a scan opening only the segments it is asked for
    /// </summary>
    public IReadOnlyList<int> ColumnIds { get; init; } = [];

    public ColumnStoreIndex? Index { get; init; }
}
