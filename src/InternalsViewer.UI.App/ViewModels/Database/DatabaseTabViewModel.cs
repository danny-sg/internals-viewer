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
using InternalsViewer.UI.App.Services;
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
                                                IDatabaseService databaseService,
                                                SettingsService settingsService)
{
    private IBufferPoolInfoProvider BufferPoolInfoProvider { get; } = bufferPoolInfoProvider;

    private IDatabaseService DatabaseService { get; } = databaseService;

    private SettingsService SettingsService { get; } = settingsService;

    public DatabaseTabViewModel Create(DatabaseSource database)
        => new(logger, database, BufferPoolInfoProvider, DatabaseService, SettingsService);
}

public sealed partial class DatabaseTabViewModel(ILogger<DatabaseTabViewModel> logger,
                                                 DatabaseSource database,
                                                 IBufferPoolInfoProvider bufferPoolInfoProvider,
                                                 IDatabaseService databaseService,
                                                 SettingsService settingsService)
    : TabViewModel, IAllocationViewModel, IAsyncDisposable
{
    private const string TooltipEnabledKey = "DatabaseTooltipEnabled";

    private ILogger<DatabaseTabViewModel> Logger { get; } = logger;

    private IDatabaseService DatabaseService { get; } = databaseService;

    private IBufferPoolInfoProvider BufferPoolInfoProvider { get; } = bufferPoolInfoProvider;

    private SettingsService SettingsService { get; } = settingsService;

    [ObservableProperty]
    private DatabaseSource _database = database;

    [ObservableProperty]
    private DatabaseFile[] _databaseFiles = [];

    [ObservableProperty]
    private bool _isTabbedView;

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _allocationLayers = [];

    public IReadOnlyList<AllocationBorder> AllocationBorders => [];

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
    private bool _isTooltipEnabled = true;

    [ObservableProperty]
    private short _fileId = 1;

    [ObservableProperty]
    private double _allocationMapHeight = 200;

    public long SequenceFrom => 0;

    public long SequenceTo => 0;

    public long PlayheadTimeUs => 0;

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

        AllocationLayers = [.. AllocationLayers];

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

            DispatcherQueue.TryEnqueue(() =>
            {
                var layer = AllocationLayers.FirstOrDefault(l => l.LayerName == "Buffer Pool");

                if (layer != null)
                {
                    layer.PageSpans =
                    [
                        ..bufferPoolPages.Clean.Select(s => new PageSpan(s, 0, 0, System.Drawing.Color.DarkCyan)),
                        ..bufferPoolPages.Dirty.Select(s => new PageSpan(s, 0, 0, System.Drawing.Color.DarkRed)),
                    ];

                    AllocationLayers = [.. AllocationLayers];
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
        =>
        [
            .. AllocationLayers.Where(w => string.IsNullOrEmpty(Filter)
                                           || w.Name.Contains(Filter, StringComparison.CurrentCultureIgnoreCase))
        ];

    partial void OnSelectedLayersChanged(ObservableCollection<AllocationLayer>? oldValue, ObservableCollection<AllocationLayer> newValue)
    {
        RefreshAllocationLayerSelection();
    }

    private void RefreshAllocationLayerSelection()
    {
        var hasSelection = SelectedLayers.Count > 0;

        var updatedLayers = AllocationLayers.Select(s =>
        {
            if (s.IsAllocationLayer)
            {
                return s;

            }

            if (hasSelection)
            {
                s.Opacity = (byte) (SelectedLayers.Any(l => l.Name == s.Name) ? 100 : 20);
            }
            else
            {
                s.Opacity = 100;
            }

            return s;
        }).ToList();

        AllocationLayers = new ObservableCollection<AllocationLayer>(updatedLayers);
    }

    public void Load(string name)
    {
        Logger.LogDebug("Loading database: {Name}", name);

        Name = name;

        DatabaseFiles =
        [
            .. Database.Files
                       .Select((f, i) => new DatabaseFile(this)
                       {
                           FileId = f.FileId,
                           Name = f.Name,
                           FileName = f.FileName,
                           Size = f.Size,
                           IsHeaderVisible = Database.Files.Count > 1,
                           IsViewToggleVisible = i == 0 && Database.Files.Count > 1
                       })
        ];

        IsLoading = true;

        _ = LoadTooltipEnabledAsync();

        // Generating the allocation layers walks the whole allocation map, so build it off the UI thread.
        _ = LoadAllocationLayersAsync();
    }

    // Started on the UI thread, so the property set (which raises PropertyChanged for the toggle binding) resumes there.
    private async Task LoadTooltipEnabledAsync()
    {
        var saved = await SettingsService.ReadSettingAsync<bool?>(TooltipEnabledKey);

        if (saved.HasValue)
        {
            IsTooltipEnabled = saved.Value;
        }
    }

    partial void OnIsTooltipEnabledChanged(bool value)
    {
        _ = SettingsService.SaveSettingAsync(TooltipEnabledKey, value);
    }

    private async Task LoadAllocationLayersAsync()
    {
        try
        {
            var layersStart = Stopwatch.GetTimestamp();

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
            var result = await DatabaseService.LoadAsync(Database.Name, Database.Connection, CancellationToken);

            if (Overlay == "Buffer Pool")
            {
                await RefreshBufferPool();
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                Database = result;

                Load(Database.Name);
            });
        }, CancellationToken);
    }

    public ValueTask DisposeAsync() => Database.Connection.DisposeAsync();
}