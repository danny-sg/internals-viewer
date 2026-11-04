using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.Plugins.Documents;

public interface IPluginDocument : IDisposable
{
    string Title { get; }

    FrameworkElement CreateView();
}
