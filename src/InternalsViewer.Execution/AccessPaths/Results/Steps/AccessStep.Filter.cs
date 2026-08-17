using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    public sealed record FilterRow(long Number, RowOutcome Outcome) : AccessStep(AccessPhase.Filter)
    {
        public IRecord? EmittedRecord { get; init; }

        public long PassedCount { get; init; }
    }
}
