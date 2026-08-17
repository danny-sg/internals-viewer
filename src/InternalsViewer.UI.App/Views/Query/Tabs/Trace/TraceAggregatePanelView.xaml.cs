using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceAggregatePanelView : UserControl
{
    public TraceAggregateViewModel? ViewModel => DataContext as TraceAggregateViewModel;

    public TraceAggregatePanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
