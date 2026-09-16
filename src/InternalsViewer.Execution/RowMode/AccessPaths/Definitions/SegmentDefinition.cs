namespace InternalsViewer.Execution.RowMode.AccessPaths.Definitions;

public sealed record SegmentDefinition(IteratorDefinition Source, string SegmentColumn) : UnaryDefinition(Source)
{
    public IReadOnlyList<string> GroupBy { get; init; } = [];
}
