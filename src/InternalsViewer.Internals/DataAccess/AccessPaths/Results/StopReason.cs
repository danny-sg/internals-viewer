namespace InternalsViewer.Internals.DataAccess.AccessPaths.Results;

/// <summary>
/// Why an access path stopped producing rows
/// </summary>
public enum StopReason
{
    /// <summary>
    /// A key left the requested range
    /// </summary>
    RangeEnded,

    /// <summary>
    /// The requested number of rows was produced
    /// </summary>
    RowGoalMet,

    /// <summary>
    /// The end of the index was reached with no further page to follow
    /// </summary>
    IndexExhausted,

    /// <summary>
    /// The end of the page was reached and the access path is limited to a single page
    /// </summary>
    PageExhausted,

    /// <summary>
    /// Execution was cancelled by the caller
    /// </summary>
    Cancelled
}
