using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceSegmentPanelView : UserControl
{
    public TraceSegmentPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public TraceSegmentViewModel? ViewModel => DataContext as TraceSegmentViewModel;
}
