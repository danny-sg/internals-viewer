using System.Threading.Tasks;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using InternalsViewer.UI.App.vNext.Views;
using InternalsViewer.UI.App.vNext.ViewModels.Tabs;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.vNext.Messages;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.UI.App.vNext.Models;
using InternalsViewer.UI.App.vNext.ViewModels.Allocation;
using Microsoft.Extensions.DependencyInjection;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Connections.Backup;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.vNext.Views.Connect;
using Microsoft.UI.Xaml.Data;

namespace InternalsViewer.UI.App.vNext;

public sealed partial class MainWindow
{
    public IServiceProvider ServiceProvider { get; }

    public MainWindow(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        InitializeComponent();

        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };

        ExtendsContentIntoTitleBar = true;

        SetTitleBar(CustomDragRegion);

        WeakReferenceMessenger.Default.Register<ConnectServerMessage>(this, async (_, m)
            => await ConnectServer(m.Value.ConnectionString));

        WeakReferenceMessenger.Default.Register<OpenPageMessage>(this, async (_, m)
            => await OpenPage(m.Value.Database, m.Value.PageAddress));
    }

    private async Task ConnectServer(string connectionString)
    {
        var connection = ServerConnectionFactory.Create(c => c.ConnectionString = connectionString);

        await AddConnection(connection);
    }

    private async Task ConnectFile(string filename)
    {
        var connection = FileConnectionFactory.Create(c => c.Filename = filename);

        await AddConnection(connection);
    }

    private async Task ConnectBackup(string filename)
    {
        var connection = BackupConnectionFactory.Create(c => c.Filename = filename);

        await AddConnection(connection);
    }

    private async Task OpenPage(DatabaseSource database, PageAddress pageAddress)
    {
        var viewModel = new PageViewModel(ServiceProvider, database);

        await viewModel.LoadPage(pageAddress);

        var content = new PageView();

        content.DataContext = viewModel;

        var tab = new TabViewItem
        {
            Name = $"Page {pageAddress.PageId}",
            Content = content
        };

        var titleBinding = new Binding { Path = new PropertyPath("Name"), Mode = BindingMode.OneWay };

        titleBinding.Source = viewModel;

        tab.SetBinding(TabViewItem.HeaderProperty, titleBinding);

        WindowTabView.TabItems.Add(tab);
        WindowTabView.SelectedItem = tab;
    }

    private async Task AddConnection(IConnectionType connection)
    {
        var databaseLoader = ServiceProvider.GetService<IDatabaseLoader>();

        if (databaseLoader == null)
        {
            throw new InvalidOperationException("Database loader not found");
        }

        var database = await databaseLoader.Load(connection.Name, connection);

        var viewModel = new DatabaseViewModel(ServiceProvider, database);

        viewModel.Name = connection.Name;

        viewModel.IsLoading = true;

        var layers = AllocationLayerBuilder.GenerateLayers(database, true);

        viewModel.Database = database;
        viewModel.Size = database.GetFileSize(1);
        viewModel.AllocationLayers = new ObservableCollection<AllocationLayer>(layers);

        viewModel.IsLoading = false;

        var content = new DatabaseView();
        content.DataContext = viewModel;

        var tab = new TabViewItem
        {
            Name = connection.Name,
            Header = connection.Name,
            Content = content
        };

        WindowTabView.TabItems.Add(tab);
        WindowTabView.SelectedItem = tab;
    }

    private void TabView_OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        sender.TabItems.Remove(args.Tab);
    }

    private async void TabView_Loaded(object sender, RoutedEventArgs e)
    {
        var tabView = sender as TabView;

        if (tabView != null)
        {
            await AddConnectTab(tabView);
        }
    }

    private async Task AddConnectTab(TabView tabView)
    {
        var content = new ConnectView();

        var viewModel = await ConnectViewModel.CreateAsync(ServiceProvider);

        content.DataContext = viewModel;

        tabView.TabItems.Add(new TabViewItem
        {
            Name = "Connect",
            Header = "Connect",
            Content = content
        });
    }

    private async Task AddDatabaseTab(TabView tabView)
    {
        var content = new ConnectView();

        var viewModel = await ConnectViewModel.CreateAsync(ServiceProvider);

        content.DataContext = viewModel;

        tabView.TabItems.Add(new TabViewItem
        {
            Name = "Connect",
            Header = "Connect",
            Content = content
        });
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}
