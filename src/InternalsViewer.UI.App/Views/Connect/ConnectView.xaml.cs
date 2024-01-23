using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Connections;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Connect;

public sealed partial class ConnectView
{
    private ConnectServerViewModelFactory ConnectServerViewModelFactory { get; }

    public ConnectView(ConnectServerViewModelFactory connectServerViewModelFactory)
    {
        ConnectServerViewModelFactory = connectServerViewModelFactory;

        InitializeComponent();

        WeakReferenceMessenger.Default.Register<NavigateMessage>(this, (_, m)
            => SelectAndNavigate(m.Value));
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private async void ConnectNavigationView_SelectionChanged(NavigationView sender,
                                                              NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
        }
        else
        {
            var selectedItem = (NavigationViewItem)args.SelectedItem;
            var selectedItemTag = (string)selectedItem.Tag;

            await Navigate(selectedItemTag);
        }
    }

    private void SelectAndNavigate(string value)
    {
        var item = ConnectNavigationView.MenuItems
                                        .OfType<NavigationViewItem>()
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
                var connectServerViewModel = ConnectServerViewModelFactory.Create();

                await connectServerViewModel.InitializeAsync();

                ContentFrame.Navigate(typeof(ConnectServerPage), connectServerViewModel);
                break;
            case "ConnectFilePage":
                var connectFileViewModel = ConnectFileViewModelFactory.Create();
                ContentFrame.Navigate(typeof(ConnectFilePage), connectFileViewModel);
                break;
            default:
                ContentFrame.Navigate(pageType);
                break;
        }
    }
}
