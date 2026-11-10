using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.Views.Query.Plan;

/// <summary>Execution plan commands hosted by the tab strip of whichever group the document is docked in</summary>
public sealed partial class QueryPlanTabCommands : UserControl
{
    public QueryPlanTabCommands()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private void OnViewMenuClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            PlanViewMenu.Create(viewModel).ShowAt((FrameworkElement)sender);
        }
    }
}
