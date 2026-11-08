using System;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using TimeTravelLicenceDialog = InternalsViewer.UI.App.Controls.Query.Trace.Dialogs.TimeTravelLicenceDialog;
using TimeTravelWarningDialog = InternalsViewer.UI.App.Controls.Query.Trace.Dialogs.TimeTravelWarningDialog;
using WinDbgInstallDialog = InternalsViewer.UI.App.Controls.Dialogs.WinDbgInstallDialog;
using InternalsViewer.Query.Recording;
using InternalsViewer.Query.FullTrace.Recording;

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

    private static ITimeTravelRecorder TimeTravel => App.GetService<ITimeTravelRecorder>();

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

        var record = item.IsChecked && await ConfirmTimeTravelAsync();

        ViewModel.QueryOptions.RecordTimeTravel = record;

        item.IsChecked = record;
    }

    private async void OnGuideModeClick(object sender, RoutedEventArgs e)
    {
        var item = (ToggleMenuFlyoutItem)sender;

        var isGuideMode = item.IsChecked;

        if (isGuideMode && !ViewModel.QueryOptions.RecordTimeTravel)
        {
            isGuideMode = await ConfirmTimeTravelAsync();

            ViewModel.QueryOptions.RecordTimeTravel = isGuideMode;
        }

        ViewModel.Layout.IsGuideMode = isGuideMode;

        item.IsChecked = isGuideMode;
    }
#pragma warning restore VSTHRD100

    private async Task<bool> ConfirmTimeTravelAsync()
    {
        if (Settings.ShowTimeTravelWarning)
        {
            var dialog = new TimeTravelWarningDialog
            {
                XamlRoot = XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }

            if (dialog.DoNotShowAgain)
            {
                Settings.ShowTimeTravelWarning = false;
            }
        }

        return await PrepareTimeTravelAsync();
    }

    private async Task<bool> PrepareTimeTravelAsync()
    {
        try
        {
            TimeTravel.PrepareRecorder();

            if (TimeTravel.IsLicenceAccepted)
            {
                return true;
            }

            var dialog = new TimeTravelLicenceDialog(TimeTravel.ReadLicence())
            {
                XamlRoot = XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }

            await TimeTravel.AcceptLicenceAsync(CancellationToken.None);

            return true;
        }
        catch (DebuggerException exception)
        {
            var dialog = exception is WinDbgNotFoundException
                ? new WinDbgInstallDialog(exception.Message)
                : new ContentDialog
                {
                    Title = "Time Travel Debugging Licence",
                    Content = exception.Message,
                    CloseButtonText = "OK"
                };

            dialog.XamlRoot = XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;

            await dialog.ShowAsync();

            return false;
        }
    }
}
