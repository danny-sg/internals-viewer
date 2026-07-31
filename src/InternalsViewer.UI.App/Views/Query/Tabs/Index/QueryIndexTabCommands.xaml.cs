using InternalsViewer.UI.App.ViewModels.Index;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Index;

/// <summary>Index commands hosted by the tab strip, alongside the allocation unit summary</summary>
public sealed partial class QueryIndexTabCommands : UserControl
{
    public IndexTabViewModel? ViewModel => DataContext as IndexTabViewModel;

    public QueryIndexTabCommands()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
