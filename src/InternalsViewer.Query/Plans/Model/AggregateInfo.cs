using InternalsViewer.Execution.AccessPaths.Aggregation;

namespace InternalsViewer.Query.Plans.Model;

public sealed record AggregateInfo
{
    public List<ColumnReference> GroupBy { get; init; } = [];

    public List<AggregateColumn> Columns { get; init; } = [];

    public bool HasUntranslatedAggregate { get; init; }

    public bool IsScalar => GroupBy.Count == 0;
}
