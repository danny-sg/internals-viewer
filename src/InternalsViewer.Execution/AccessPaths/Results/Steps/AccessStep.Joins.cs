using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results.Steps;

public abstract partial record AccessStep
{
    /// <summary>
    /// A join announced what it is about to do, narrating the start of a composed access path
    /// </summary>
    public sealed record JoinStart(string Description) : AccessStep(AccessPhase.Ranges);

    /// <summary>
    /// A matching outer and inner row pair result was emitted by a join
    /// </summary>
    public sealed record JoinEmit(int PairNumber) : AccessStep(AccessPhase.Walk)
    {
        public IRecord? OuterRecord { get; init; }

        public IRecord? InnerRecord { get; init; }

        public bool IsFromBuffer { get; init; }

        /// <summary>
        /// The row found no partner and reaches the output only because the join preserves its side
        /// </summary>
        public bool IsUnmatched { get; init; }
    }
}
