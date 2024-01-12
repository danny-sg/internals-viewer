using InternalsViewer.UI.App.vNext.ViewModels.Tabs;
using System;
using Windows.Storage.Pickers;
using InternalsViewer.UI.App.vNext.Controls.Connections;

namespace InternalsViewer.UI.App.vNext.Views;

public sealed partial class ConnectView
{
    public ConnectView()
    {
        InitializeComponent();
    }

    public ConnectViewModel ViewModel => (ConnectViewModel)DataContext;

    private async void ServerConnectionControl_OnConnectRequested(object? sender, ServerConnectEventArgs e)
    {
        await ViewModel.SaveSettings(e.Settings);

        await ViewModel.Parent.ConnectServerCommand.ExecuteAsync(e.ConnectionString);
    }

    private async void HeaderTile_OnClick(object? sender, RoutedEventArgs e)
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
            ViewModel.Parent.ConnectFileCommand.Execute(file.Path);
        }
    }
}
