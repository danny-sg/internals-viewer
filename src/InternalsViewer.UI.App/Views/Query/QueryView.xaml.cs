using System;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using TimeTravelLicenceDialog = InternalsViewer.UI.App.Controls.Dialogs.TimeTravelLicenceDialog;
using TimeTravelWarningDialog = InternalsViewer.UI.App.Controls.Dialogs.TimeTravelWarningDialog;
using WinDbgInstallDialog = InternalsViewer.UI.App.Controls.Dialogs.WinDbgInstallDialog;
using InternalsViewer.Query.Recording;
using InternalsViewer.Query.FullTrace.Recording;

namespace InternalsViewer.UI.App.Views.Query;

public sealed partial class QueryView : Page, IDisposable
{
    public QueryView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        Unloaded += OnUnloaded;
    }

    public QueryViewModel ViewModel => (QueryViewModel)DataContext;

    private static SettingsViewModel Settings => App.GetService<SettingsViewModel>();

    private static ITimeTravelRecorder TimeTravel => App.GetService<ITimeTravelRecorder>();

    public void Dispose()
    {
        // x:Bind listens to the view model, which outlives the view, so the view stays rooted until tracking stops
        Bindings.StopTracking();

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

    private async void OnRecordTimeTravelClick(object sender, RoutedEventArgs e)
    {
        var item = (ToggleMenuFlyoutItem)sender;

        var record = item.IsChecked;

        if (record && Settings.ShowTimeTravelWarning)
        {
            var dialog = new TimeTravelWarningDialog
            {
                XamlRoot = XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            record = await dialog.ShowAsync() == ContentDialogResult.Primary;

            if (record && dialog.DoNotShowAgain)
            {
                Settings.ShowTimeTravelWarning = false;
            }
        }

        if (record)
        {
            record = await PrepareTimeTravelAsync();
        }

        ViewModel.QueryOptions.RecordTimeTravel = record;

        item.IsChecked = record;
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
