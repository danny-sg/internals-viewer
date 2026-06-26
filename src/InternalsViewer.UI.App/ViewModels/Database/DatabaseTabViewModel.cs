using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Editing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using DatabaseFile = InternalsViewer.UI.App.Models.DatabaseFile;

namespace InternalsViewer.UI.App.ViewModels.Database;

public sealed class DatabaseTabViewModelFactory(ILogger<DatabaseTabViewModel> logger, 
                                                IDatabaseService databaseService)
{
    private IDatabaseService DatabaseService { get; } = databaseService;

    public DatabaseTabViewModel Create(DatabaseSource database)
        => new(logger, database, DatabaseService);
}

public sealed partial class DatabaseTabViewModel(ILogger<DatabaseTabViewModel> logger, 
                                                 DatabaseSource database, 
                                                 IDatabaseService databaseService) 
    : TabViewModel, IAllocationViewModel, IAsyncDisposable
{
    private ILogger<DatabaseTabViewModel> Logger { get; } = logger;

    private IDatabaseService DatabaseService { get; } = databaseService;

    [ObservableProperty]
    private DatabaseSource _database = database;

    [ObservableProperty]
    private DatabaseFile[] _databaseFiles = [];

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _allocationLayers = [];

    [ObservableProperty]
    private PfsChain _pfsChain = new();

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _selectedLayers = [];

    [ObservableProperty]
    private int _extentCount;

    [ObservableProperty]
    private AllocationOverViewModel _allocationOver = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridAllocationLayers))]
    private string _filter = string.Empty;

    [ObservableProperty]
    private bool _isDetailVisible = true;

    [ObservableProperty]
    private string _overlay = "Overlay";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Overlay))]
    private bool _hasOverlay;

    public bool IsPfsVisible => Overlay == "PFS";

    [ObservableProperty]
    private bool _isQueryReplayVisible;

    [ObservableProperty]
    private bool _isTooltipEnabled;

    [ObservableProperty]
    private short _fileId = 1;

    [ObservableProperty]
    private double _allocationMapHeight = 200;

    public long SequenceFrom => 0;

    public long SequenceTo => 0;

    [RelayCommand]
    private void SetOverlay(string overlay)
    {
        if (overlay == Overlay)
        {
            return;
        }

        Overlay = overlay;
        HasOverlay = Overlay != "Overlay";

        foreach (var layer in AllocationLayers)
        {
            if (HasOverlay)
            {
                layer.Opacity = (byte)(layer.LayerName == Overlay ? 100 : 0);
            }
            else
            {
                layer.Opacity = (byte)(string.IsNullOrEmpty(layer.LayerName) ? 100 : 0);
            }
        }
    }

    [RelayCommand]
    private void OpenPage(PageAddress pageAddress)
    {
        WeakReferenceMessenger.Default.Send(new OpenPageMessage(new OpenPageRequest(Database, pageAddress)));
    }

    [RelayCommand]
    private void OpenQueryReplay()
    {
        WeakReferenceMessenger.Default.Send(new OpenQueryReplayMessage(Database));
    }

    public List<AllocationLayer> GridAllocationLayers
        => AllocationLayers.Where(w => string.IsNullOrEmpty(Filter) 
                                       || w.Name.ToLower().Contains(Filter.ToLower())).ToList();

    public void Load(string name)
    {
        Logger.LogDebug("Loading database: {Name}", name);

        Name = name;

        DatabaseFiles = Database.Files
                                .Select(f => new DatabaseFile(this) { FileId = f.FileId, Size = f.Size })
                                .ToArray();
        IsLoading = true;

        var layersStart = Stopwatch.GetTimestamp();

        var layers = AllocationLayerBuilder.GenerateLayers(Database, true);

        Logger.LogDebug("Generated allocation layers in: {Elapsed}", Stopwatch.GetElapsedTime(layersStart));

        ExtentCount = Database.GetFilePageCount(1) / 8;

        AllocationLayers = new ObservableCollection<AllocationLayer>(layers);

        PfsChain = Database.Pfs.First().Value;

        IsLoading = false;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await Task.Run(async () =>
        {
            var result = await DatabaseService.LoadAsync(Database.Name, Database.Connection);

            DispatcherQueue.TryEnqueue(() =>
            {
                Database = result;

                Load(Database.Name);
            });
        });
    }

    public ValueTask DisposeAsync() => Database.Connection.DisposeAsync();
}