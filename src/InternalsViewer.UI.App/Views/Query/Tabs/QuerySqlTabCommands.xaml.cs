using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>SQL editor commands hosted by the tab strip</summary>
public sealed partial class QuerySqlTabCommands : UserControl
{
    public QuerySqlTabCommands()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;
}
