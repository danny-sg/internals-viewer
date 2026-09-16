using InternalsViewer.Execution.Common.AccessPaths.Aggregation;

namespace InternalsViewer.Execution.BatchMode.AccessPaths.Definitions;

/// <summary>
/// Hash Aggregate reading and producing batches
/// </summary>
public sealed record BatchHashAggregateDefinition(IteratorDefinition Source) : UnaryDefinition(Source), IBatchDefinition
{
    public IReadOnlyList<string> GroupBy { get; init; } = [];

    public IReadOnlyList<AggregateColumn> Aggregates { get; init; } = [];

    public long RowEstimate { get; init; }

    public int? BucketBits { get; init; }
}
