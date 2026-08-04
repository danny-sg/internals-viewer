using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceTabView : UserControl
{
    public TraceTabViewModel? ViewModel => DataContext as TraceTabViewModel;

    public TraceTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public string RunLabel(bool isRunning) => isRunning ? "Stop" : "Run";

    public string RunToEndLabel(bool isRunningToEnd) => isRunningToEnd ? "Stop" : "Run to End";

    public string RunGlyph(bool isRunning) => isRunning ? "" : "";
}
