using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay;
using InternalsViewer.Replay.Events;
using DatabaseFile = InternalsViewer.UI.App.Models.DatabaseFile;

namespace InternalsViewer.UI.App.ViewModels.QueryReplay;

public sealed class QueryReplayViewModelFactory(ILogger<QueryReplayViewModel> logger, 
                                                QueryCaptureExecutor queryCaptureExecutor)
{
    public QueryReplayViewModel Create(DatabaseSource database) => new(logger, queryCaptureExecutor, database);
}

public sealed partial class QueryReplayViewModel : TabViewModel, IAllocationViewModel
{
    public ILogger<QueryReplayViewModel> Logger { get; }

    public QueryCaptureExecutor QueryCaptureExecutor { get; }

    public DatabaseSource Database { get; }

    [ObservableProperty]
    private string sql = string.Empty;

    [ObservableProperty]
    private bool isPfsVisible = false;

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> selectedLayers = [];

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> allocationLayers = [];

    [ObservableProperty]
    private PfsChain pfsChain = new();

    [ObservableProperty]
    private bool isTooltipEnabled = true;

    [ObservableProperty]
    private bool isQueryExecuting;

    [ObservableProperty]
    private bool includeLocks = false;

    [ObservableProperty]
    private bool includeIo = false;

    [ObservableProperty]
    private bool isClearBufferPool = true;

    [ObservableProperty]
    private int extentCount;

    [ObservableProperty]
    private double allocationMapHeight = 200;

    [ObservableProperty]
    private DatabaseFile[] databaseFiles = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEvents))]
    private List<EngineEvent> events = [];

    [ObservableProperty]
    private HashSet<int> systemObjectIds = [];

    public Microsoft.UI.Xaml.Visibility HasEvents
        => Events.Count > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    [ObservableProperty]
    private bool canExecute => !string.IsNullOrWhiteSpace(Sql) && !IsQueryExecuting;

    private List<AllocationLayer> ObjectLayers { get; set; }

    /// <inheritdoc/>
    public QueryReplayViewModel(ILogger<QueryReplayViewModel> logger,
                                QueryCaptureExecutor queryCaptureExecutor,
                                DatabaseSource database)
    {
        Logger = logger;
        QueryCaptureExecutor = queryCaptureExecutor;
        Database = database;

        Name = "Query Replay";

        DatabaseFiles = database.Files
            .Select(f => new DatabaseFile(this) { FileId = f.FileId, Size = f.Size })
            .ToArray();

        ObjectLayers = AllocationLayerBuilder.GenerateLayers(database, true);

        ExtentCount = database.GetFileSize(1) / 8;

        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);

        PfsChain = database.Pfs.First().Value;

        systemObjectIds = database.AllocationUnits
            .Where(u => u.IsSystem)
            .Select(u => u.ObjectId)
            .ToHashSet();
    }

    [RelayCommand]
    private async Task ExecuteQuery()
    {
        IsQueryExecuting = true;

        var results = await QueryCaptureExecutor.TraceQuery(Sql,
                                                            Database,
                                                            clearBufferPool: true);

        var layers = GetEventsAllocationLayer(results);

        var names = Database.AllocationUnits
            .GroupBy(u => u.ObjectId)
            .ToDictionary(g => g.Key, g => g.First().DisplayName);

        foreach (var e in results.Where(e => e.ObjectId > 0))
        {
            e.ObjectName = names.TryGetValue(e.ObjectId, out var n) ? n : $"(Object Id: {e.ObjectId})";
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            Events = results;
            AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);

            foreach (var layer in layers)
            {
                AllocationLayers.Add(layer);
            }

            SelectedLayers = new ObservableCollection<AllocationLayer>(layers);
            IsQueryExecuting = false;
        });
    }

    private List<AllocationLayer> GetEventsAllocationLayer(List<EngineEvent> engineEvents)
    {
        var maxFileId = databaseFiles.Max(d => d.FileId);

        var ioLayer = new AllocationLayer
        {
            Name = "I/O",
            Colour = Color.Navy,
            IsVisible = true,
        };

        var pageLayer = new AllocationLayer
        {
            Name = "Page",
            Colour = Color.DarkCyan,
            IsVisible = true,
        };

        var lockLayer = new AllocationLayer
        {
            Name = "Lock",
            Colour = Color.DarkRed,
            IsVisible = true,
        };

        foreach (var e in engineEvents)
        {
            switch (e)
            {
                case IoEvent ioEvent:
                    if (ioEvent.PageAddress is { FileId: > 0 } && ioEvent.PageAddress.Value.FileId <= Database.Files.Count)
                    {
                        ioLayer.PageSpans.Add(new PageSpan(ioEvent.PageAddress.Value, ioEvent.SequenceId));
                    }

                    break;
                case PageEvent pageEvent:
                    if (pageEvent.PageAddress is { FileId: > 0 } && pageEvent.PageAddress.Value.FileId <= Database.Files.Count)
                    {
                        pageLayer.PageSpans.Add(new PageSpan(pageEvent.PageAddress.Value, pageEvent.SequenceId));

                    }

                    break;
                case LockEvent lockEvent:
                    var pageAddress = lockEvent.RowIdentifier?.PageAddress ?? lockEvent.PageAddress;

                    if (pageAddress is { FileId: > 0 } && pageAddress.Value.FileId <= maxFileId)
                    {
                        lockLayer.PageSpans.Add(new PageSpan(pageAddress.Value, lockEvent.SequenceId));
                    }

                    break;
            }
        }

        return new List<AllocationLayer> { ioLayer, pageLayer, lockLayer };
    }
}
