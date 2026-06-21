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
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay;
using InternalsViewer.Replay.Events;
using InternalsViewer.UI.App.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using DatabaseFile = InternalsViewer.UI.App.Models.DatabaseFile;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed class QueryViewModelFactory(ILogger<QueryViewModel> logger,
                                          QueryCaptureExecutor queryCaptureExecutor,
                                          SettingsService settingsService)
{
    public QueryViewModel Create(DatabaseSource database) => new(logger,
                                                                 queryCaptureExecutor,
                                                                 settingsService,
                                                                 database);
}

public sealed partial class QueryViewModel : TabViewModel, IAllocationViewModel
{
    private const string UncheckedSettingsKey = "EventFilterUnchecked";

    private static readonly HashSet<string> DefaultUnchecked =
    [
        "lock_acquired/SCH_M/Metadata",
        "lock_acquired/SCH_S/Metadata",
        "lock_acquired/SCH_S/Object",
        "lock_acquired/S/Database",
        "lock_released/SCH_M/Metadata",
        "lock_released/SCH_S/Metadata",
        "lock_released/SCH_S/Object",
        "lock_released/S/Database",
    ];

    public ILogger<QueryViewModel> Logger { get; }

    public QueryCaptureExecutor QueryCaptureExecutor { get; }

    public DatabaseSource Database { get; }

    private SettingsService SettingsService { get; }

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
    private bool isClearBufferPool = false;

    [ObservableProperty]
    private int extentCount;

    [ObservableProperty] 
    private string message;

    [ObservableProperty] 
    public bool isError;

    partial void OnIsErrorChanged(bool value) => OnPropertyChanged(nameof(ResultBrush));

    [ObservableProperty]
    private double allocationMapHeight = 200;

    [ObservableProperty]
    private DatabaseFile[] databaseFiles = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEvents))]
    private List<EngineEvent> events = [];

    [ObservableProperty]
    private List<EngineEvent> filteredEvents = [];

    [ObservableProperty]
    private HashSet<int> systemObjectIds;

    [ObservableProperty]
    private long sequenceFrom;

    [ObservableProperty]
    private long sequenceTo;

    [ObservableProperty]
    private ObservableCollection<EventFilterNode> filterNodes = [];

    [ObservableProperty]
    private bool includeSystemObjects;

    partial void OnIncludeSystemObjectsChanged(bool value) => RefreshFilteredEvents();

    public Microsoft.UI.Xaml.Visibility HasEvents
        => Events.Count > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public SolidColorBrush ResultBrush => IsError
        ? new SolidColorBrush(Colors.Red)
        : (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];

    [ObservableProperty]
    private bool canExecute => !string.IsNullOrWhiteSpace(Sql) && !IsQueryExecuting;

    private List<AllocationLayer> ObjectLayers { get; set; }

    public QueryViewModel(ILogger<QueryViewModel> logger,
                          QueryCaptureExecutor queryCaptureExecutor,
                          SettingsService settingsService,
                          DatabaseSource database)
    {
        Logger = logger;
        QueryCaptureExecutor = queryCaptureExecutor;
        SettingsService = settingsService;
        Database = database;

        Name = "Query";

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

        var results = await QueryCaptureExecutor.TraceQuery(Sql, Database, clearBufferPool: true);

        if (!results.IsSuccess)
        {
            IsError = true;
            Message = results.Message;

            IsQueryExecuting = false;
            return;
        }

        IsError = false;
        Message = $"({results.RowCount} rows affected)";

        var names = Database.AllocationUnits
                            .GroupBy(u => u.ObjectId)
                            .ToDictionary(g => g.Key, g => g.First().DisplayName);

        foreach (var e in results.EngineEvents.Where(e => e.ObjectId > 0))
        {
            e.ObjectName = names.TryGetValue(e.ObjectId, out var n) ? n : $"(Object Id: {e.ObjectId})";
        }

        DispatcherQueue.TryEnqueue(async void () =>
        {
            Events = results.EngineEvents;

            IsQueryExecuting = false;

            await BuildFilterTreeAsync();
        });

        RefreshLayers(results.EngineEvents);
    }

    private void RefreshLayers(List<EngineEvent> engineEvents)
    {
        var layers = GetEventsAllocationLayer(engineEvents);

        DispatcherQueue.TryEnqueue(async () =>
        {
            AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);

            foreach (var layer in layers)
            {
                AllocationLayers.Add(layer);
            }

            SelectedLayers = new ObservableCollection<AllocationLayer>(layers);
        });
    }

    private async Task BuildFilterTreeAsync()
    {
        var savedUnchecked = await SettingsService.ReadSettingAsync<List<string>>(UncheckedSettingsKey);

        var uncheckedPaths = savedUnchecked is { Count: > 0 }
            ? new HashSet<string>(savedUnchecked)
            : DefaultUnchecked;

        var root = new EventFilterNode { Label = "Events" };

        var eventNodes = Events
            .GroupBy(e => e.Name)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var eventNode = new EventFilterNode { Label = g.Key, Parent = root };

                var children = g
                    .Select(e => e.Description)
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct()
                    .OrderBy(d => d)
                    .Select(d =>
                    {
                        var path = $"{g.Key}/{d}";
                        var child = new EventFilterNode
                        {
                            Label = d,
                            Parent = eventNode,
                            IsChecked = !uncheckedPaths.Contains(path)
                        };
                        child.PropertyChanged += OnFilterNodeChanged;
                        return child;
                    });

                foreach (var child in children)
                {
                    eventNode.Children.Add(child);
                }

                eventNode.RefreshCheckedState();
                eventNode.PropertyChanged += OnFilterNodeChanged;

                return eventNode;
            });

        foreach (var node in eventNodes)
        {
            root.Children.Add(node);
        }

        root.RefreshCheckedState();
        root.PropertyChanged += OnFilterNodeChanged;

        FilterNodes = new ObservableCollection<EventFilterNode> { root };

        RefreshFilteredEvents();
    }

    private void OnFilterNodeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RefreshFilteredEvents();
        _ = SaveUncheckedAsync();
    }

    private async Task SaveUncheckedAsync()
    {
        var root = FilterNodes.FirstOrDefault();
        if (root is null)
        {
            return;
        }

        var uncheckedPaths = root.Children
            .SelectMany(eventNode => eventNode.Children
                .Where(c => c.IsChecked == false)
                .Select(c => $"{eventNode.Label}/{c.Label}"))
            .ToList();

        foreach (var eventNode in root.Children.Where(n => n.IsChecked == false && n.Children.Count == 0))
        {
            uncheckedPaths.Add(eventNode.Label);
        }

        await SettingsService.SaveSettingAsync(UncheckedSettingsKey, uncheckedPaths);
    }

    private IEnumerable<EngineEvent> ApplyNodeFilter(IEnumerable<EngineEvent> source)
    {
        var root = FilterNodes.FirstOrDefault();

        if (root is null || root.IsChecked == true)
        {
            return source;
        }

        if (root.IsChecked == false)
        {
            return [];
        }

        var eventNodes = root.Children;

        return source.Where(e =>
        {
            var eventNode = eventNodes.FirstOrDefault(n => n.Label == e.Name);

            if (eventNode is null)
            {
                return false;
            }

            if (eventNode.IsChecked == false)
            {
                return false;
            }

            if (eventNode.IsChecked == true || eventNode.Children.Count == 0)
            {
                return true;
            }

            return eventNode.Children.Any(c => c.IsChecked == true && c.Label == e.Description);
        });
    }

    public void RefreshFilteredEvents()
    {
        var source = !IncludeSystemObjects && SystemObjectIds.Count > 0
            ? Events.Where(e => e.ObjectId == 0 || !SystemObjectIds.Contains(e.ObjectId))
            : Events.AsEnumerable();

        FilteredEvents = [.. ApplyNodeFilter(source)];

        RefreshLayers(FilteredEvents);
    }

    private List<AllocationLayer> GetEventsAllocationLayer(List<EngineEvent> engineEvents)
    {
        var maxFileId = databaseFiles.Max(d => d.FileId);

        var ioLayer = new AllocationLayer { Name = "I/O", Colour = ColourConstants.IoColour, IsVisible = true };
        var pageLayer = new AllocationLayer { Name = "Page", Colour = ColourConstants.PageColour, IsVisible = true };
        var lockLayer = new AllocationLayer { Name = "Lock", Colour = ColourConstants.LockColour, IsVisible = true };

        var systemIoLayer = new AllocationLayer { Name = "I/O (System)", Colour = ColourConstants.SystemIoColour, IsVisible = true };
        var systemPageLayer = new AllocationLayer { Name = "Page (System)", Colour = ColourConstants.SystemPageColour, IsVisible = true };
        var systemLockLayer = new AllocationLayer { Name = "Lock (System)", Colour = ColourConstants.SystemLockColour, IsVisible = true };

        foreach (var e in engineEvents)
        {
            if (e.PageAddress is { FileId: > 0 }
                && e.PageAddress.Value.FileId <= maxFileId)
            {
                var isSystemObject = SystemObjectIds.Contains(e.ObjectId);

                switch (e)
                {
                    case IoEvent ioEvent:
                        if (isSystemObject)
                        {
                            systemIoLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId));
                        }
                        else
                        {
                            ioLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId, e.SequenceId));
                        }

                        break;

                    case PageEvent pageEvent:
                        if (isSystemObject)
                        {
                            systemPageLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId));
                        }
                        else
                        {
                            pageLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId, e.SequenceId));
                        }

                        break;
                    case WaitEvent waitEvent:
                        break;

                    case LockEvent lockEvent:
                        if (isSystemObject)
                        {
                            systemLockLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId));
                        }
                        else
                        {
                            lockLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId, e.SequenceId));
                        }

                        break;
                }
            }

        }

        return [ioLayer, pageLayer, lockLayer, systemIoLayer, systemPageLayer, systemLockLayer];
    }
}
