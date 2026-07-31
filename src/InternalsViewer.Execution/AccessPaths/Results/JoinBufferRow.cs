using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Results;

/// <summary>
/// What a join has decided about a row it is holding
/// </summary>
public enum JoinRowState
{
    /// <summary>
    /// Read, but not yet compared against the other side
    /// </summary>
    Pending,

    /// <summary>
    /// Found a partner and will be paired
    /// </summary>
    Matched,

    /// <summary>
    /// Judged and acted on, so the join no longer holds it
    /// </summary>
    Finished
}

/// <summary>
/// A row a join is holding, and what it has decided about it
/// </summary>
/// <remarks>
/// The state is set from the comparison that judges the row, which is the point sort order or the loop structure has proven whether a
/// partner can still be found.
/// </remarks>
public readonly record struct JoinBufferRow(IRecord Record, JoinRowState State)
{
    public bool IsMatched => State == JoinRowState.Matched;
}
