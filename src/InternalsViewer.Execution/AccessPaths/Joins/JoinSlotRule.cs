namespace InternalsViewer.Execution.AccessPaths.Results.Joins;

/// <summary>
/// What a join requires of one side for a row to reach the output
/// </summary>
public enum JoinSlotRule
{
    /// <summary>
    /// A row must have been found on this side
    /// </summary>
    Present,

    /// <summary>
    /// No row must have been found on this side
    /// </summary>
    Absent,

    /// <summary>
    /// The join returns the row either way
    /// </summary>
    Any
}