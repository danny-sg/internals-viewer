using System;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

public sealed partial class QuerySqlTabView : UserControl, IDisposable
{
    public QuerySqlTabView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public void Dispose()
    {
        DataContextChanged -= OnDataContextChanged;

        Bindings.StopTracking();

        SqlEditor.Dispose();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) => Bindings.Update();

    private void OnOpenAllocations(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.Layout.IsAllocationsVisible = true;
        }
    }

    private void OnOpenPlan(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.Layout.IsExecutionPlanVisible = true;
        }
    }

    private void OnOpenEvents(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.Layout.IsEventsVisible = true;
        }
    }
}
