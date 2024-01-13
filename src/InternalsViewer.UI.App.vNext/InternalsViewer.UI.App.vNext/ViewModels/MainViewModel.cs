using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.UI.App.vNext.Models;
using InternalsViewer.UI.App.vNext.ViewModels.Allocation;
using InternalsViewer.UI.App.vNext.ViewModels.Tabs;
using Microsoft.Extensions.DependencyInjection;

namespace InternalsViewer.UI.App.vNext.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public IServiceProvider ServiceProvider { get; }

    [ObservableProperty]
    private TabViewModel? selectedTab;

    [ObservableProperty]
    private ObservableCollection<TabViewModel> tabs = new();

    /// <inheritdoc/>
    public MainViewModel(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public async Task InitializeAsync()
    {
        var connectViewModel = await ConnectViewModel.CreateAsync(this);

        Tabs.Add(connectViewModel);

        SelectedTab = Tabs[0];
    }

    [RelayCommand]
    private async Task ConnectServer(string connectionString)
    {
        await AddDatabase(connectionString);

        //Calibrate();
    }

    [RelayCommand]
    private async Task ConnectFile(string filename)
    {
        await AddFile(filename);

        //Calibrate();
    }

    private async Task AddFile(string filename)
    {
        var connection = FileConnectionFactory.Create(c => c.Filename = filename);

        await AddConnection(connection);
    }

    [RelayCommand]
    private void CloseTab(TabViewModel tab)
    {
        Tabs.Remove(tab);

        if(tab == SelectedTab && Tabs.Any())
        {
            SelectedTab = Tabs[0];
        }
    }

    public async Task AddDatabase(string connectionString)
    {
        var connection = ServerConnectionFactory.Create(c => c.ConnectionString = connectionString);

        await AddConnection(connection);
    }

    private async Task AddConnection(IConnectionType connection)
    {
        var databaseLoader = ServiceProvider.GetService<IDatabaseLoader>();

        if(databaseLoader == null)
        {
            throw new InvalidOperationException("Database loader not found");
        }

        var database = await databaseLoader.Load(connection.Name, connection);

        var viewModel = new DatabaseViewModel(this, database);

        viewModel.Name = connection.Name;

        Tabs.Add(viewModel);

        SelectedTab = viewModel;

        viewModel.IsLoading = true;

        var layers = AllocationLayerBuilder.GenerateLayers(database, true);

        viewModel.Database = database;
        viewModel.Size = database.GetFileSize(1);
        viewModel.AllocationLayers = new ObservableCollection<AllocationLayer>(layers);

        viewModel.IsLoading = false;
    }

    public void Calibrate()
    {
        var database = new DatabaseSource(null!);

        var viewModel = new DatabaseViewModel(this, database);  

        viewModel.Name = "Calibration";

        var layers = CalibrationBuilder.GenerateLayers();

        viewModel.Database = database;
        viewModel.Size = 1000;
        viewModel.AllocationLayers = new ObservableCollection<AllocationLayer>(layers);

        Tabs.Add(viewModel);

        SelectedTab = viewModel;
    }

    public async Task OpenPage(DatabaseSource database, PageAddress pageAddress)
    {
        var viewModel = new PageViewModel(this, database);

        Tabs.Add(viewModel);

        SelectedTab = viewModel;

        await viewModel.LoadPage(pageAddress);
    }   
}