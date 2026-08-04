using InternalsViewer.Execution.AccessPaths.Joins;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes an operator that reads two inputs and combines their rows
/// </summary>
/// <remarks>
/// A join's <see cref="IteratorDefinition.Residual"/> is the predicate the join itself applies once a pair has matched on its keys, which
/// showplan writes as the Hash probe residual, the Merge residual or the Nested Loops predicate. Each side carries its own residual, which
/// is a different thing entirely, applied by the access path before the join ever sees the row.
/// </remarks>
public abstract record JoinDefinition : IteratorDefinition
{
    public JoinType JoinType { get; init; } = JoinType.Inner;
}
