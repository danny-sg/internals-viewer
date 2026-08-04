using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceHeldRowsPanelView : UserControl
{
    public TraceHeldRowsViewModel? ViewModel => DataContext as TraceHeldRowsViewModel;

    public TraceHeldRowsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
