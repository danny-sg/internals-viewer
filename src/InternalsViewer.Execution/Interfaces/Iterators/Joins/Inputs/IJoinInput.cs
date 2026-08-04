using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.Interfaces.Iterators.Joins.Inputs;

/// <summary>
/// One of the two inputs a join reads from
/// </summary>
/// <remarks>
/// A join is defined by what it does with two inputs rather than by what those inputs are, so both sides of a merge join and the outer
/// side of a loop join are the same shape. Only an input a loop rebinds needs more than this.
/// </remarks>
public interface IJoinInput
{
    IStepIterator Iterator { get; }

    AccessStrategy? Strategy { get; }

    /// <summary>
    /// Rows this input has returned that the join is still holding
    /// </summary>
    IReadOnlyList<JoinBufferRow> Buffer { get; }
}
