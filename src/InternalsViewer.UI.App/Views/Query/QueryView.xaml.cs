using System;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

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

        ViewModel.QueryOptions.RecordTimeTravel = record;

        item.IsChecked = record;
    }
}
