using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

/// <summary>Trace commands hosted by the tab strip</summary>
public sealed partial class TraceTabCommands : UserControl
{
    public TraceTabViewModel? ViewModel => DataContext as TraceTabViewModel;

    public TraceTabCommands()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
