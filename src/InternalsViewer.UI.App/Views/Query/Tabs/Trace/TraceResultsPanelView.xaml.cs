using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceResultsPanelView : UserControl
{
    public TraceTabViewModel? ViewModel => DataContext as TraceTabViewModel;

    public TraceResultsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
