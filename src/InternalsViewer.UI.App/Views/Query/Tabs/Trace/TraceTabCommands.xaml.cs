using InternalsViewer.UI.App.ViewModels.Query.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Trace;

public sealed partial class TraceTabCommands : UserControl
{
    public TraceTabCommands()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public TraceTabViewModel? ViewModel => DataContext as TraceTabViewModel;
}
