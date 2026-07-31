using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.Plan;

/// <summary>Execution plan commands hosted by the tab strip of whichever group the document is docked in</summary>
public sealed partial class QueryPlanTabCommands : UserControl
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public QueryPlanTabCommands()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }
}
