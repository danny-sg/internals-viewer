using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>Allocation map commands hosted by the tab strip</summary>
public sealed partial class QueryAllocationTabCommands : UserControl
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public QueryAllocationTabCommands()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
