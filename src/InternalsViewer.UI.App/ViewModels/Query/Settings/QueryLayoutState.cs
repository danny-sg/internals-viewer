using System.Collections.Generic;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.UI.App.ViewModels.Docking;

namespace InternalsViewer.UI.App.ViewModels.Query.Settings;

public sealed class QueryLayoutState
{
    public DockNode? Root { get; set; }

    public bool TimelineVisible { get; set; } = true;

    public bool CropToQuery { get; set; } = true;

    public bool IncludeSystemObjects { get; set; }

    public bool IncludeLock { get; set; } = true;

    public bool IncludeWait { get; set; } = true;

    public bool IncludeLatch { get; set; }

    public bool IncludeMemory { get; set; }

    public bool IncludeCallstack { get; set; }

    public List<LockModeCategory>? LockModeCategories { get; set; }
}
