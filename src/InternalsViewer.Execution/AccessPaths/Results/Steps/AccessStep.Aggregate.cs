using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    public sealed record AggregateStart(bool IsScalar) : AccessStep(AccessPhase.Group)
    {
        public string Aggregates { get; init; } = string.Empty;

        public string GroupBy { get; init; } = string.Empty;
    }

    public sealed record AggregateGroup(long Number, string Key) : AccessStep(AccessPhase.Group);

    public sealed record AggregateRow(long Number, long GroupRows) : AccessStep(AccessPhase.Accumulate)
    {
        public IRecord? EmittedRecord { get; init; }

        public string Running { get; init; } = string.Empty;
    }

    public sealed record AggregateEmit(long Number, string Key) : AccessStep(AccessPhase.Emit)
    {
        public IRecord? EmittedRecord { get; init; }

        public string Values { get; init; } = string.Empty;

        public long GroupRows { get; init; }
    }

    public sealed record HashAggregateBatch(long Number, int RowCount) : AccessStep(AccessPhase.Accumulate)
    {
        public long InputRowCount { get; init; }

        public long Groups { get; init; }

        public long NewGroups { get; init; }

        public int BucketCount { get; init; }

        public IReadOnlyList<int> Fill { get; init; } = [];

        public string LastKey { get; init; } = string.Empty;

        public string Running { get; init; } = string.Empty;
    }

    public sealed record HashAggregate(int Bucket, uint Hash, int Entry) : AccessStep(AccessPhase.Accumulate)
    {
        public AccessKey Key { get; init; }

        public int ChainLength { get; init; }

        public int BucketCount { get; init; }

        public bool IsNewGroup { get; init; }

        public long Number { get; init; }

        public long GroupRows { get; init; }

        public string Running { get; init; } = string.Empty;
    }

    public sealed record ComputeRow(long Number) : AccessStep(AccessPhase.Compute)
    {
        public IRecord? EmittedRecord { get; init; }

        public string Values { get; init; } = string.Empty;
    }
}
