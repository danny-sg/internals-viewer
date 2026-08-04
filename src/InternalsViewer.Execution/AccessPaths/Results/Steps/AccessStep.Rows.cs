using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results;

public abstract partial record AccessStep
{
    /// <summary>
    /// A row was examined
    /// </summary>
    public sealed record Row(int Slot, RowOutcome Outcome) : AccessStep(AccessPhase.Walk)
    {
        public bool HasResidual { get; init; }

        public bool HasRange { get; init; } = true;

        public IRecord? EmittedRecord { get; init; }

        /// <summary>
        /// The row was read to find where a matched group ended, so it belongs to the next comparison rather than the current one
        /// </summary>
        public bool IsReadAhead { get; init; }

        /// <summary>
        /// The row was taken from a slot named outright rather than found by walking, so nothing was tested to reach it
        /// </summary>
        public bool IsFetched { get; init; }
    }

    public sealed record Output(long Number) : AccessStep(AccessPhase.Walk)
    {
        public IRecord? EmittedRecord { get; init; }
    }

    public sealed record RowRun(int FromSlot, int ToSlot, RowOutcome Outcome) : AccessStep(AccessPhase.Walk)
    {
        public int Count { get; init; }

        public bool HasResidual { get; init; }

        public bool HasRange { get; init; } = true;

        public int EmitCount { get; init; }
    }
}
