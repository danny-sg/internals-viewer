using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    public sealed record InputStart(int Number, int Count) : AccessStep(AccessPhase.Ranges);

    public sealed record ConcatRow(long Number, int InputNumber) : AccessStep(AccessPhase.Walk)
    {
        public IRecord? EmittedRecord { get; init; }
    }
}
