using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    public sealed record SortCollect(long Number) : AccessStep(AccessPhase.Walk)
    {
        public bool IsRetained { get; init; } = true;
    }

    public sealed record Sorted(long Rows) : AccessStep(AccessPhase.Walk);

    public sealed record SortRow(long Number) : AccessStep(AccessPhase.Walk)
    {
        public IRecord? EmittedRecord { get; init; }
    }

    public sealed record SortDuplicate(long Number) : AccessStep(AccessPhase.Walk)
    {
        public int Count { get; init; } = 1;
    }
}
