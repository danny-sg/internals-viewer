using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>Dock document hosting the SQL editor for the active query.</summary>
public sealed partial class SqlDocumentView : UserControl
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public SqlDocumentView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    private void OnOpenAllocations(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.IsAllocationsVisible = true;
        }
    }

    private void OnOpenPlan(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.IsExecutionPlanVisible = true;
        }
    }

    private void OnOpenEvents(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.IsEventsVisible = true;
        }
    }
}
