namespace InternalsViewer.Internals.DataAccess.AccessPaths.Results;

/// <summary>
/// Result of examining a single row
/// </summary>
public enum RowOutcome
{
    /// <summary>
    /// Row satisfied every predicate and is returned
    /// </summary>
    Match,

    /// <summary>
    /// Row was read but rejected by a residual predicate
    /// </summary>
    NoMatch,

    /// <summary>
    /// Residual predicate evaluated to unknown, typically because of a NULL operand
    /// </summary>
    Unknown,

    /// <summary>
    /// Row is a ghost and was skipped without evaluation
    /// </summary>
    Ghost
}