using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceOperatorResultsPanelView : UserControl
{
    public TraceOperatorViewModel? ViewModel => DataContext as TraceOperatorViewModel;

    public TraceOperatorResultsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
