using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceRowStreamPanelView : UserControl
{
    public TraceRowStreamViewModel? ViewModel => DataContext as TraceRowStreamViewModel;

    public TraceRowStreamPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
