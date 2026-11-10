using System;
using CommunityToolkit.WinUI;
using InternalsViewer.UI.App.Controls.Query.Trace.Dialogs;
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

    private void OnTracePromptLoaded(object sender, RoutedEventArgs e)
    {
        if (((InfoBar)sender).FindDescendant("CloseButton") is { } closeButton)
        {
            ToolTipService.SetToolTip(closeButton, "Do not show again");
        }
    }

    private void OnTracePromptCloseClick(InfoBar sender, object args) => ViewModel?.DisableTracePrompt();

#pragma warning disable VSTHRD100
    private async void OnFullTraceClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.IsTracePromptVisible = false;

        viewModel.QueryOptions.RecordTimeTravel = await TimeTravelConsent.ConfirmAsync(XamlRoot);
    }

    private async void OnGuideModeClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.IsTracePromptVisible = false;

        var isConfirmed = await TimeTravelConsent.ConfirmAsync(XamlRoot);

        viewModel.QueryOptions.RecordTimeTravel = isConfirmed;

        viewModel.Layout.IsGuideMode = isConfirmed;
    }
#pragma warning restore VSTHRD100

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
