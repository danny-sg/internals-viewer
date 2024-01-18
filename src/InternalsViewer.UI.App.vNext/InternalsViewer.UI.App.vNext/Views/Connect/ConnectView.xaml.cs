using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.vNext.Controls.Connections;
using InternalsViewer.UI.App.vNext.Messages;
using InternalsViewer.UI.App.vNext.ViewModels.Connections;
using InternalsViewer.UI.App.vNext.ViewModels.Tabs;
using InternalsViewer.UI.App.vNext.Services;

namespace InternalsViewer.UI.App.vNext.Views.Connect;

public sealed partial class ConnectView
{
    public ConnectView()
    {
        InitializeComponent();

        WeakReferenceMessenger.Default.Register<NavigateMessage>(this, (_, m)
            => SelectAndNavigate(m.Value));
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

    private async void ConnectNavigationView_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender,
                                                        Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
        }
        else
        {
            var selectedItem = (Microsoft.UI.Xaml.Controls.NavigationViewItem)args.SelectedItem;
            var selectedItemTag = (string)selectedItem.Tag;

            await Navigate(selectedItemTag);
        }
    }

    private void SelectAndNavigate(string value)
    {
        var item = ConnectNavigationView.MenuItems
                                        .OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>()
                                        .First(i => (string)i.Tag == value);

        ConnectNavigationView.SelectedItem = item;
    }

    private async Task Navigate(string value)
    {
        var namespaceName = GetType().Namespace;

        var pageName = $"{namespaceName}.{value}";

        var pageType = Type.GetType(pageName);

        switch (value)
        {
            case "ConnectServerPage":
                var connectServerViewModel = ViewModel.GetService<ServerConnectionViewModel>();

                await connectServerViewModel.InitializeAsync();

                ContentFrame.Navigate(typeof(ConnectServerPage), connectServerViewModel);
                break;
            default:
                ContentFrame.Navigate(pageType);
                break;
        }
    }
}
