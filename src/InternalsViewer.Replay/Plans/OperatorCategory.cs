namespace InternalsViewer.Replay.Plans;

/// <summary>
/// Broad grouping of plan operators for visualisation: data access (IO), joins, row transformations
/// and buffering/blocking operators.
/// </summary>
public enum OperatorCategory
{
    DataAccess,
    Join,
    Transformation,
    Buffer
}
