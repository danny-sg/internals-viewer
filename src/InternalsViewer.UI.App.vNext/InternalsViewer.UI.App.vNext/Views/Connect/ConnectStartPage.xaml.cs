using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.vNext.Controls.Connections;
using InternalsViewer.UI.App.vNext.Messages;
using InternalsViewer.UI.App.vNext.ViewModels.Tabs;
using System;
using Windows.Storage.Pickers;

namespace InternalsViewer.UI.App.vNext.Views.Connect;

public sealed partial class ConnectStartPage
{
    public ConnectStartPage()
    {
        InitializeComponent();
    }

    public ConnectViewModel ViewModel => (ConnectViewModel)DataContext;

    private async void ServerConnectionControl_OnConnectRequested(object? sender, ServerConnectEventArgs e)
    {

        await ViewModel.SaveSettings(e.Settings);
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
            WeakReferenceMessenger.Default.Send(new ConnectFileMessage(file.Path));
        }
    }

    private void ConnectSqlServerHeaderTile_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage("ConnectServerPage"));
    }
}
