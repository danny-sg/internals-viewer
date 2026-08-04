using InternalsViewer.Execution.AccessPaths.Joins;

namespace InternalsViewer.Execution.Interfaces.Iterators.Joins;

/// <summary>
/// One of the two inputs a join reads from
/// </summary>
public interface IJoinInput
{
    IIterator Iterator { get; }

    /// <summary>
    /// Rows this input has returned that the join is still holding
    /// </summary>
    IReadOnlyList<JoinBufferRow> Buffer { get; }
}
