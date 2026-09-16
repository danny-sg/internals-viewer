using InternalsViewer.Execution.Common.AccessPaths.Aggregation;

namespace InternalsViewer.Execution.RowMode.AccessPaths.Definitions;

public sealed record StreamAggregateDefinition(IteratorDefinition Source) : UnaryDefinition(Source)
{
    public IReadOnlyList<string> GroupBy { get; init; } = [];

    public IReadOnlyList<AggregateColumn> Aggregates { get; init; } = [];

    public bool IsScalar => GroupBy.Count == 0;
}
