using System;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Sql;

public sealed partial class QuerySqlTabView : UserControl, IDisposable
{
    public QuerySqlTabView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private QueryViewModel? Subscribed { get; set; }

    public void Dispose()
    {
        DataContextChanged -= OnDataContextChanged;

        Subscribe(null);

        Bindings.StopTracking();

        SqlEditor.Dispose();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Subscribe(ViewModel);

        Bindings.Update();
    }

    private void Subscribe(QueryViewModel? viewModel)
    {
        if (Subscribed is not null)
        {
            Subscribed.SqlTextImported -= OnSqlTextImported;
        }

        Subscribed = viewModel;

        if (viewModel is not null)
        {
            viewModel.SqlTextImported += OnSqlTextImported;
        }
    }

    private void OnSqlTextImported(string sql) => SqlEditor.LoadQuery(sql);

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
