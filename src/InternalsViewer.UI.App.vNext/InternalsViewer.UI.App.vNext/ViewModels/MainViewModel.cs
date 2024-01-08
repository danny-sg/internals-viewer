using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public CurrentConnection Connection { get; }

    [ObservableProperty]
    private TabViewModel selectedTab;

    [ObservableProperty]
    private ObservableCollection<TabViewModel> tabs = new();

    /// <inheritdoc/>
    public MainViewModel(IDatabaseLoader databaseLoader, CurrentConnection connection)
    {
        DatabaseLoader = databaseLoader;
        Connection = connection;

        Tabs.Add(new GetStartedViewModel(this) { Name = "Get Started"});

        SelectedTab = Tabs[0];
    }

    [RelayCommand]
    private async Task ConnectServer(string databaseName)
    {
        await AddDatabase(databaseName);
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
        Connection.ConnectionString = "Server=.;Database=master;Integrated Security=true;TrustServerCertificate=True";
        Connection.DatabaseName = "TestDatabase";

        var database = await DatabaseLoader.Load("TestDatabase");

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
        var database = new DatabaseDetail();

        var viewModel = new DatabaseViewModel(this, database);  

        viewModel.Name = "Calibration";

        var layers = CalibrationBuilder.GenerateLayers();

        viewModel.Database = database;
        viewModel.Size = 1000;
        viewModel.AllocationLayers = new ObservableCollection<AllocationLayer>(layers);

        Tabs.Add(viewModel);

        SelectedTab = viewModel;
    }

    public async Task OpenPage(DatabaseDetail database, PageAddress pageAddress)
    {
        var viewModel = new PageViewModel(this, database);

        Tabs.Add(viewModel);

        SelectedTab = viewModel;

        await viewModel.LoadPage(pageAddress);
    }   
}