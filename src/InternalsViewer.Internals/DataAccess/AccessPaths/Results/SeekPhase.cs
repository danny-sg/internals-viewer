namespace InternalsViewer.Internals.DataAccess.AccessPaths.Results;

public enum SeekPhase
{
    Ranges,
    Allocation,
    Descent,
    Position,
    Walk,
    Complete
}