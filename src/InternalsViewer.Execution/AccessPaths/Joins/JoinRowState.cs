namespace InternalsViewer.Execution.AccessPaths.Joins;

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