using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Internals.Connections.Server;
using DatabaseFile = InternalsViewer.UI.App.Models.DatabaseFile;

namespace InternalsViewer.UI.App.ViewModels.Database;

public sealed class DatabaseTabViewModelFactory(ILogger<DatabaseTabViewModel> logger,
                                                IBufferPoolInfoProvider bufferPoolInfoProvider,
                                                IDatabaseService databaseService)
{
    private IBufferPoolInfoProvider BufferPoolInfoProvider { get; } = bufferPoolInfoProvider;

    private IDatabaseService DatabaseService { get; } = databaseService;

    public DatabaseTabViewModel Create(DatabaseSource database)
        => new(logger, database, BufferPoolInfoProvider, DatabaseService);
}

public sealed partial class DatabaseTabViewModel(ILogger<DatabaseTabViewModel> logger,
                                                 DatabaseSource database,
                                                 IBufferPoolInfoProvider bufferPoolInfoProvider,
                                                 IDatabaseService databaseService)
    : TabViewModel, IAllocationViewModel, IAsyncDisposable
{
    private ILogger<DatabaseTabViewModel> Logger { get; } = logger;

    private IDatabaseService DatabaseService { get; } = databaseService;

    private IBufferPoolInfoProvider BufferPoolInfoProvider { get; } = bufferPoolInfoProvider;

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
    [NotifyPropertyChangedFor(nameof(IsPfsVisible))]
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

    public bool IsServerConnection => Database.Connection is ServerConnectionType;

    [RelayCommand]
    private async Task SetOverlay(string overlay)
    {
        var changed = overlay != Overlay;

        Overlay = overlay;

        HasOverlay = Overlay != "Overlay";

        if (!changed)
        {
            return;
        }

        foreach (var layer in AllocationLayers)
        {
            if (!string.IsNullOrEmpty(layer.LayerName) && layer.LayerName != Overlay)
            {
                // Overlay not selected
                layer.Opacity = 0;

                continue;
            }

            if (Overlay == "PFS")
            {
                layer.Opacity = (byte)(layer.LayerName == Overlay ? 100 : 20);

                continue;
            }

            if (Overlay == "Buffer Pool")
            {
                layer.Opacity = (byte)(layer.LayerName == Overlay ? 100 : 20);

                continue;
            }

            if (HasOverlay)
            {
                layer.Opacity = (byte)(layer.LayerName == Overlay ? 100 : 0);
            }
            else
            {
                layer.Opacity = (byte)(string.IsNullOrEmpty(layer.LayerName) ? 100 : 0);
            }
        }

        AllocationLayers = new ObservableCollection<AllocationLayer>(AllocationLayers);

        if (Overlay == "Buffer Pool")
        {
            await RefreshBufferPool();
        }
    }

    private async Task RefreshBufferPool()
    {
        try
        {
            var bufferPoolPages = await BufferPoolInfoProvider.GetBufferPoolEntries(Database);

            // RefreshBufferPool is also called from Refresh's Task.Run, so this continuation can be on a
            // background thread; marshal the bound-collection updates onto the UI thread.
            DispatcherQueue.TryEnqueue(() =>
            {
                var layer = AllocationLayers.FirstOrDefault(l => l.LayerName == "Buffer Pool");

                if (layer != null)
                {
                    layer.SinglePages = bufferPoolPages.Dirty;

                    AllocationLayers = new ObservableCollection<AllocationLayer>(AllocationLayers);
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh buffer pool overlay for database: {Name}", Database.Name);
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
        WeakReferenceMessenger.Default.Send(new OpenQueryMessage(Database));
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

        // Generating the allocation layers walks the whole allocation map, so build it off the UI thread.
        _ = LoadAllocationLayersAsync();
    }

    private async Task LoadAllocationLayersAsync()
    {
        try
        {
            var layersStart = Stopwatch.GetTimestamp();

            // Heavy allocation-map walk on a background thread; the bound assignments below run on the UI
            // continuation (Load is called on the UI thread).
            var (layers, extentCount, pfsChain) = await Task.Run(() =>
            {
                var generated = AllocationLayerBuilder.GenerateLayers(Database, true);
                var extents = Database.GetFilePageCount(1) / 8;
                var pfs = Database.Pfs.First().Value;

                return (generated, extents, pfs);
            });

            Logger.LogDebug("Generated allocation layers in: {Elapsed}", Stopwatch.GetElapsedTime(layersStart));

            AllocationLayers = new ObservableCollection<AllocationLayer>(layers);
            ExtentCount = extentCount;
            PfsChain = pfsChain;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate allocation layers for database: {Name}", Database.Name);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await Task.Run(async () =>
        {
            var result = await DatabaseService.LoadAsync(Database.Name, Database.Connection);

            if (Overlay == "Buffer Pool")
            {
                await RefreshBufferPool();
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                Database = result;

                Load(Database.Name);
            });
        });
    }

    public ValueTask DisposeAsync() => Database.Connection.DisposeAsync();
}