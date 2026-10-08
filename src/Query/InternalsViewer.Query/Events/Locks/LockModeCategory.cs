namespace InternalsViewer.Query.Events.Locks;

/// <summary>
/// Categorisation of lock modes by grouped intent
/// </summary>
public enum LockModeCategory
{
    None,
    Read,
    Update,
    Write,
    Schema,
    Range,
    Bulk
}