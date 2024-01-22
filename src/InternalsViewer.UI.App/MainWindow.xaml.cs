using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using InternalsViewer.UI.App.Views;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Connections.Backup;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.Views.Connect;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Linq;
using InternalsViewer.UI.App.Models.Connections;
using DatabaseFile = InternalsViewer.UI.App.Models.DatabaseFile;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Database;
using InternalsViewer.UI.App.ViewModels.Page;
using InternalsViewer.UI.App.ViewModels.Connections;

namespace InternalsViewer.UI.App;

public sealed partial class MainWindow
{
    private IServiceProvider ServiceProvider { get; }

    private IDatabaseLoader DatabaseLoader { get; }

    private TabViewItem? ConnectTab { get; set; }

    private MainViewModel ViewModel { get; }

    public PageTabViewModelFactory PageTabViewModelFactory { get; }

    public DatabaseTabViewModelFactory DatabaseTabViewModelFactory { get; }

    public ConnectServerViewModelFactory ConnectServerViewModelFactory { get; }


    public MainWindow(IServiceProvider serviceProvider,
                      IDatabaseLoader databaseLoader,
                      MainViewModel mainViewModel,
                      PageTabViewModelFactory pageTabViewModelFactory,
                      DatabaseTabViewModelFactory databaseTabViewModelFactory,
                      ConnectServerViewModelFactory connectServerViewModelFactory)
    {
        ServiceProvider = serviceProvider;
        DatabaseLoader = databaseLoader;
        
        ViewModel = mainViewModel;
        PageTabViewModelFactory = pageTabViewModelFactory;
        DatabaseTabViewModelFactory = databaseTabViewModelFactory;
        ConnectServerViewModelFactory = connectServerViewModelFactory;

        InitializeComponent();

        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };

        ExtendsContentIntoTitleBar = true;

        SetTitleBar(CustomDragRegion);

        WeakReferenceMessenger.Default.Register<ConnectServerMessage>(this, (_, m)
            => m.Reply(ConnectServer(m.ConnectionString, m.Recent)));

        WeakReferenceMessenger.Default.Register<ConnectFileMessage>(this, (_, m)
            => m.Reply(ConnectFile(m.Filename, m.Recent)));

        WeakReferenceMessenger.Default.Register<OpenPageMessage>(this, (_, m)
            => m.Reply(OpenPage(m.Request.Database, m.Request.PageAddress)));
    }

    private async Task<bool> ConnectServer(string connectionString, RecentConnection recent)
    {
        var connection = ServerConnectionFactory.Create(c => c.ConnectionString = connectionString);

        await AddConnection(connection);

        await ViewModel.AddRecentConnectionCommand.ExecuteAsync(recent);

        return true;
    }

    private async Task<bool> ConnectFile(string filename, RecentConnection recent)
    {
        var connection = FileConnectionFactory.Create(c => c.Filename = filename);

        await AddConnection(connection);

        await ViewModel.AddRecentConnectionCommand.ExecuteAsync(recent);

        return true;
    }

    private async Task ConnectBackup(string filename)
    {
        var connection = BackupConnectionFactory.Create(c => c.Filename = filename);

        await AddConnection(connection);
    }

    private async Task<bool> OpenPage(DatabaseSource database, PageAddress pageAddress)
    {
        var viewModel = PageTabViewModelFactory.Create(database);

        await viewModel.LoadPage(pageAddress);

        var content = new PageView();

        content.DataContext = viewModel;

        var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/PageTabIcon.svg"));

        var tab = new TabViewItem
        {
            Name = $"Page {pageAddress.PageId}",
            Content = content,
            IconSource = new ImageIconSource { ImageSource = svg }
        };

        var titleBinding = new Binding { Path = new PropertyPath("Name"), Mode = BindingMode.OneWay };

        titleBinding.Source = viewModel;

        tab.SetBinding(TabViewItem.HeaderProperty, titleBinding);

        WindowTabView.TabItems.Add(tab);
        WindowTabView.SelectedItem = tab;

        ConnectTab!.IsClosable = true;

        return true;
    }

    private async Task AddConnection(IConnectionType connection)
    {
        var database = await DatabaseLoader.Load(connection.Name, connection);

        var viewModel = DatabaseTabViewModelFactory.Create(database);

        viewModel.Load(connection.Name);

        var content = new DatabaseView();

        content.DataContext = viewModel;

        var svg = new SvgImageSource(new Uri("ms-appx:///Assets/TabIcons/DatabaseTabIcon.svg"));

        var tab = new TabViewItem
        {
            Name = connection.Name,
            Header = connection.Name,
            IconSource = new ImageIconSource { ImageSource = svg },
            Content = content
        };

        WindowTabView.TabItems.Add(tab);
        WindowTabView.SelectedItem = tab;

        ConnectTab!.IsClosable = true;
    }

    private void TabView_OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        // Hide the Connect tab rather than close it
        if (args.Tab == ConnectTab)
        {
            ConnectTab.Visibility = Visibility.Collapsed;
        }
        else
        {
            sender.TabItems.Remove(args.Tab);
        }

        // If all tabs have been closed, show the Connect tab
        if (sender.TabItems.Count == 1)
        {
            ConnectTab!.Visibility = Visibility.Visible;
            ConnectTab!.IsClosable = false;
            sender.SelectedItem = ConnectTab;
        }
    }

    private void TabView_Loaded(object sender, RoutedEventArgs e)
    {
        var tabView = sender as TabView;

        if (tabView != null)
        {
            ConnectTab = AddConnectTab(tabView);
            ConnectTab.IsClosable = false;
        }
    }

    private TabViewItem AddConnectTab(TabView tabView)
    {
        var content = new ConnectView(ConnectServerViewModelFactory);

        content.DataContext = ViewModel;

        var connectTab = new TabViewItem
        {
            Name = "Connect",
            Header = "Connect",
            Content = content
        };

        tabView.TabItems.Add(connectTab);

        return connectTab;
    }

    public async Task InitializeAsync()
    {
        await ViewModel.InitializeAsync();
    }
}
