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

#pragma warning disable CA1822
    public string RunLabel(bool isRunning) => isRunning ? "Pause" : "Run";


    public string RunToEndLabel(bool isRunningToEnd) => isRunningToEnd ? "Stop" : "Run to end";

    public string RunGlyph(bool isRunning) => isRunning ? "\uE769" : "\uE768";
#pragma warning restore CA1822
}
