namespace InternalsViewer.Execution.BatchMode.AccessPaths.Definitions;

public sealed record BatchComputeScalarDefinition(IteratorDefinition Source) : UnaryDefinition(Source), IBatchDefinition
{
    public IReadOnlyList<ComputedColumn> Columns { get; init; } = [];
}
