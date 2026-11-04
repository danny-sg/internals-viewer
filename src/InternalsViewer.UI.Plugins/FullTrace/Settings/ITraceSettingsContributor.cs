using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.Plugins.FullTrace.Settings;

public interface ITraceSettingsContributor
{
    string Header { get; }

    string Description { get; }

    FrameworkElement CreateContent();
}
