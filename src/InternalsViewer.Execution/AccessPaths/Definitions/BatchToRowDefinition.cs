namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record BatchToRowDefinition : IteratorDefinition
{
    public ColumnstoreScanDefinition Source { get; init; } = new();
}
