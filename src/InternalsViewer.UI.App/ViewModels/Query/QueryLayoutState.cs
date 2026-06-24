using InternalsViewer.UI.App.ViewModels.Docking;

namespace InternalsViewer.UI.App.ViewModels.Query;

/// <summary>Persisted snapshot of the query view's dock layout and panel visibility.</summary>
public sealed class QueryLayoutState
{
    public DockNodeDto? Root { get; set; }

    public bool TimelineVisible { get; set; } = true;

    public bool SettingsOpen { get; set; }
}
