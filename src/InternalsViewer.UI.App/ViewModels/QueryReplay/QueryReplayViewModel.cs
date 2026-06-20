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

public sealed class QueryReplayViewModelFactory(ILogger<QueryReplayViewModel> logger, QueryCaptureExecutor queryCaptureExecutor)
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
    private int extentCount;

    [ObservableProperty]
    private double allocationMapHeight = 200;

    [ObservableProperty]
    private DatabaseFile[] databaseFiles = [];

    [ObservableProperty] private List<EngineEvent> events = [];

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
    }

    [RelayCommand]
    private async Task ExecuteQuery()
    {
        IsQueryExecuting = true;

        var results = await QueryCaptureExecutor.GetQueryEngineEvents(Sql,
                                                                      Database.Connection.GetConnectionString(),
                                                                      true);

        var layers = GetEventsAllocationLayer(results);

        DispatcherQueue.TryEnqueue(() =>
        {
            Events = results;
            AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers.Union(layers));
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
                    if (ioEvent.FileId <= Database.Files.Count)
                    {
                        ioLayer.SinglePages.Add(new PageAddress(ioEvent.FileId, ioEvent.PageId));
                    }

                    break;
                case PageEvent pageEvent:
                    if (pageEvent.FileId <= Database.Files.Count)
                    {
                        pageLayer.SinglePages.Add(new PageAddress(pageEvent.FileId, pageEvent.PageId));

                    }

                    break;
                case LockEvent lockEvent:
                    if (lockEvent.PageId > 0 && lockEvent.FileId <= maxFileId)
                    {
                        lockLayer.SinglePages.Add(new PageAddress(lockEvent.FileId, lockEvent.PageId));
                    }

                    break;
            }
        }

        return new List<AllocationLayer> { pageLayer, lockLayer };
    }
}
