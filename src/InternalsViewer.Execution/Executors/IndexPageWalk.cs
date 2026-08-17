using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.Executors;

/// <summary>
/// Index page walk definition
/// </summary>
/// <remarks>
/// Includes navigation in a page walk
/// </remarks>
internal sealed record IndexPageWalk : PageWalk
{
    public SeekBounds Bounds { get; init; } = SeekBounds.All;

    public ScanDirection Direction { get; init; } = ScanDirection.Forward;

    public bool IsContinuation { get; init; }

    public bool IsForward => Direction == ScanDirection.Forward;
}
