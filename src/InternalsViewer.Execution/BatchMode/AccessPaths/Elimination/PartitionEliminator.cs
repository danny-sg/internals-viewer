using InternalsViewer.Execution.Common.AccessPaths.Predicates;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Execution.BatchMode.AccessPaths.Elimination;

public sealed class PartitionEliminator(AccessPredicate? predicate)
{
    private AccessPredicate? Predicate { get; } = predicate;

    public EliminationResult Evaluate(RowGroup rowGroup) => EliminationResult.Kept;
}
