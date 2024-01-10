using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Connections;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Providers;
using InternalsViewer.UI.App.vNext.Models;
using InternalsViewer.UI.App.vNext.ViewModels.Allocation;
using InternalsViewer.UI.App.vNext.ViewModels.Tabs;

namespace InternalsViewer.UI.App.vNext.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public IDatabaseLoader DatabaseLoader { get; }

    [ObservableProperty]
    private TabViewModel selectedTab;

    [ObservableProperty]
    private ObservableCollection<TabViewModel> tabs = new();

    /// <inheritdoc/>
    public MainViewModel(IDatabaseLoader databaseLoader)
    {
        DatabaseLoader = databaseLoader;

        Tabs.Add(new GetStartedViewModel(this) { Name = "Get Started"});

        SelectedTab = Tabs[0];
    }

    [RelayCommand]
    private async Task ConnectServer(string databaseName)
    {
        await AddDatabase(databaseName);

        //Calibrate();
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

    public async Task AddDatabase(string name)
    {
       var connectionString = "Server=.;Database=master;Integrated Security=true;TrustServerCertificate=True";

        var connection = ServerConnectionFactory.Create(c => c.ConnectionString = connectionString);

        var database = await DatabaseLoader.Load("TestDatabase", connection);

        var viewModel = new DatabaseViewModel(this, database);

        viewModel.Name = name;

        var layers = AllocationLayerBuilder.GenerateLayers(database, true);

        viewModel.Database = database;
        viewModel.Size = database.GetFileSize(1);
        viewModel.AllocationLayers = new ObservableCollection<AllocationLayer>(layers);

        Tabs.Add(viewModel);

        SelectedTab = viewModel;
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