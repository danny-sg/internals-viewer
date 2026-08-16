using InternalsViewer.Execution.AccessPaths.Aggregation;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record HashAggregateDefinition(IteratorDefinition Source) : UnaryDefinition(Source)
{
    public IReadOnlyList<string> GroupBy { get; init; } = [];

    public IReadOnlyList<AggregateColumn> Aggregates { get; init; } = [];

    public long RowEstimate { get; init; }

    public int? BucketBits { get; init; }
}
