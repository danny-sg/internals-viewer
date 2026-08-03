using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.AccessPaths.Results;

public abstract partial record AccessStep
{
    /// <summary>
    /// A correlated seek value was bound from an outer row and the inner path will descend for it
    /// </summary>
    public sealed record Rebind(int RebindNumber, AccessKey Key) : AccessStep(AccessPhase.Descent)
    {
        /// <summary>
        /// The row identifier bound instead of a key, when the inner side is a heap
        /// </summary>
        public RowIdentifier? RowIdentifier { get; init; }
    }

    /// <summary>
    /// A loop join weighed the rows a rebind returned against what the join type requires
    /// </summary>
    public sealed record JoinVerdict(JoinDecision Decision) : AccessStep(AccessPhase.Walk);
}
