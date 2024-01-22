using InternalsViewer.UI.App.ViewModels.Connections;
using Microsoft.UI.Xaml.Navigation;
using System;
using Windows.Storage.Pickers;

namespace InternalsViewer.UI.App.Views.Connect;

public sealed partial class ConnectFilePage
{
    private ConnectFileViewModel ViewModel => (ConnectFileViewModel)DataContext;

    public ConnectFilePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ConnectFileViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var openPicker = new FileOpenPicker();

        var window = (Application.Current as App)!.Window;

        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

        openPicker.ViewMode = PickerViewMode.List;

        openPicker.FileTypeFilter.Add(".mdf");

        var file = await openPicker.PickSingleFileAsync();

        if (file != null)
        {
            ViewModel.Filename = file.Path;
            ViewModel.IsValid = true;
        }
    }
}
