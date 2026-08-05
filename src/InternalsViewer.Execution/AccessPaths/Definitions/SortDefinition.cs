namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record SortDefinition(IteratorDefinition Source) : UnaryDefinition(Source)
{
    public IReadOnlyList<SortKey> Keys { get; init; } = [];

    public bool IsDistinct { get; init; }

    public long? TopCount { get; init; }
}