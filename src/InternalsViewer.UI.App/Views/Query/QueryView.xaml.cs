using System;
using InternalsViewer.UI.App.Controls.Query.Trace.Dialogs;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query;

public sealed partial class QueryView : Microsoft.UI.Xaml.Controls.Page, IDisposable
{
    public QueryView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        Unloaded += OnUnloaded;
    }

    public QueryViewModel ViewModel => (QueryViewModel)DataContext;

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    public Visibility GetRulerVisibility(bool isPinned, bool isAvailable, bool isTrace, bool isTraceTimelineVisible)
        => IsRulerShown(isPinned, isAvailable, isTrace, isTraceTimelineVisible) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GetTransportVisibility(bool isPinned, bool isAvailable, bool isTrace, bool isTraceTimelineVisible, bool isGuideMode)
        => IsRulerShown(isPinned, isAvailable, isTrace, isTraceTimelineVisible) || isGuideMode ? Visibility.Visible : Visibility.Collapsed;

    private static bool IsRulerShown(bool isPinned, bool isAvailable, bool isTrace, bool isTraceTimelineVisible)
        => isPinned && isAvailable && (!isTrace || isTraceTimelineVisible);

    public void Dispose()
    {
        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until tracking stops
        Bindings.StopTracking();

        PinnedRuler.Dispose();

        PinnedTransport.Dispose();

        (DataContext as QueryViewModel)?.Dispose();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not QueryViewModel viewModel)
        {
            return;
        }

        DockHostControl.CaptureSizes();

        _ = viewModel.SaveLayoutAsync();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();
    }

#pragma warning disable VSTHRD100
    private async void OnRecordTimeTravelClick(object sender, RoutedEventArgs e)
    {
        var item = (ToggleMenuFlyoutItem)sender;

        var record = item.IsChecked && await TimeTravelConsent.ConfirmAsync(XamlRoot);

        ViewModel.QueryOptions.RecordTimeTravel = record;

        item.IsChecked = record;
    }

    private async void OnGuideModeClick(object sender, RoutedEventArgs e)
    {
        var item = (ToggleMenuFlyoutItem)sender;

        var isGuideMode = item.IsChecked;

        if (isGuideMode && !ViewModel.QueryOptions.RecordTimeTravel)
        {
            isGuideMode = await TimeTravelConsent.ConfirmAsync(XamlRoot);

            ViewModel.QueryOptions.RecordTimeTravel = isGuideMode;
        }

        ViewModel.Layout.IsGuideMode = isGuideMode;

        item.IsChecked = isGuideMode;
    }
#pragma warning restore VSTHRD100
}
