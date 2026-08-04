using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

/// <summary>
/// The hash table of one hash match, shown under the build side that fills it
/// </summary>
public sealed partial class TraceHashTablePanelView : UserControl
{
    public TraceHashTableViewModel? ViewModel => DataContext as TraceHashTableViewModel;

    public TraceHashTablePanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
