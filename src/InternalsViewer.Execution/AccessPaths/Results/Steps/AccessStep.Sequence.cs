using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    public sealed record RankRow(long Number) : AccessStep(AccessPhase.Rank)
    {
        public IRecord? EmittedRecord { get; init; }

        public string Values { get; init; } = string.Empty;

        public bool IsNewPartition { get; init; }

        public bool IsNewValue { get; init; }

        public long PartitionRow { get; init; }
    }
}
