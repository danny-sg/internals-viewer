namespace InternalsViewer.Execution.RowMode.AccessPaths.Definitions;

public sealed record ComputeScalarDefinition(IteratorDefinition Source) : UnaryDefinition(Source)
{
    public IReadOnlyList<ComputedColumn> Columns { get; init; } = [];
}
