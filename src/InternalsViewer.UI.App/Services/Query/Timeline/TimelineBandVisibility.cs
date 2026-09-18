namespace InternalsViewer.UI.App.Services.Query.Timeline;

internal readonly record struct TimelineBandVisibility(bool ShowLocks, bool ShowLatches, bool ShowWaits);
