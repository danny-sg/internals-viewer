using InternalsViewer.Execution.AccessPaths.Aggregation;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record StreamAggregateDefinition(IteratorDefinition Source) : UnaryDefinition(Source)
{
    public IReadOnlyList<string> GroupBy { get; init; } = [];

    public IReadOnlyList<AggregateColumn> Aggregates { get; init; } = [];

    public bool IsScalar => GroupBy.Count == 0;
}
