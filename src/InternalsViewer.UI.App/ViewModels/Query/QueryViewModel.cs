using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Replay;
using InternalsViewer.Replay.Events.EventTypes;
using InternalsViewer.Replay.Plans;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Controls.SqlEditor;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Replay.Parsing;
using DatabaseFile = InternalsViewer.UI.App.Models.DatabaseFile;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed class QueryViewModelFactory(ILogger<QueryViewModel> logger,
                                          QueryRunner queryRunner,
                                          SettingsService settingsService)
{
    public QueryViewModel Create(DatabaseSource database) => new(logger,
                                                                 queryRunner,
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

    public QueryRunner QueryRunner { get; }

    public DatabaseSource Database { get; }

    private SettingsService SettingsService { get; }

    [ObservableProperty] 
    private bool isError;

    [ObservableProperty] 
    private string message;

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
    [NotifyPropertyChangedFor(nameof(ActiveOperatorVisibility))]
    private bool isTimelinePlaying;

    [ObservableProperty]
    private bool includeLocks = false;

    [ObservableProperty]
    private bool includeIo = false;

    [ObservableProperty]
    private int extentCount;

    [ObservableProperty]
    public bool cropToQuery = true;

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
    public double? startTime;

    [ObservableProperty]
    public double? endTime;

    /// <summary>Sequence id under the timeline playhead; drives the active operator.</summary>
    [ObservableProperty]
    private long playheadSequence;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimelineRowHeight))]
    [NotifyPropertyChangedFor(nameof(TimelineSplitterVisibility))]
    private bool isTimelineVisible = true;

    public GridLength TimelineRowHeight
        => IsTimelineVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public Visibility TimelineSplitterVisibility
        => IsTimelineVisible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The dock layout (tabs + splits) shown in the document region of the query view.</summary>
    public DockLayoutViewModel Dock { get; }

    /// <summary>The Events tab; used to bring it to the front when navigating from the timeline.</summary>
    private DocumentViewModel EventsDocument { get; }

    /// <summary>Raised when the timeline asks the events grid to scroll to / select an event.</summary>
    public event Action<EngineEvent>? EventNavigationRequested;

    /// <summary>Brings the Events tab forward and asks the grid to navigate to the given event.</summary>
    public void NavigateToEvent(EngineEvent engineEvent)
    {
        Dock.Activate(EventsDocument);
        EventNavigationRequested?.Invoke(engineEvent);
    }

    [ObservableProperty]
    private bool isEventSelectionPanelOpen;

    [ObservableProperty]
    private ObservableCollection<EventFilterNode> filterNodes = [];

    [ObservableProperty]
    private ObservableCollection<ExecutionPlan> executionPlans = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorName))]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorObject))]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorIcon))]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorVisibility))]
    private PlanNode? activePlanNode;

    partial void OnPlayheadSequenceChanged(long value) => UpdateActiveOperator(value);

    public string ActiveOperatorName => ActivePlanNode?.PhysicalOperator ?? string.Empty;

    public string ActiveOperatorObject
    {
        get
        {
            if (ActivePlanNode is null)
            {
                return string.Empty;
            }

            var table = ActivePlanNode.Table?.Trim('[', ']');

            if (string.IsNullOrEmpty(table))
            {
                return string.Empty;
            }

            var index = ActivePlanNode.Index?.Trim('[', ']');

            return string.IsNullOrEmpty(index) ? table : $"{table}.{index}";
        }
    }

    public ImageSource? ActiveOperatorIcon
        => ActivePlanNode is null ? null : new SvgImageSource(PlanIconResolver.Resolve(ActivePlanNode));

    public Visibility ActiveOperatorVisibility
        => ActivePlanNode is not null && IsTimelinePlaying ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateActiveOperator(long sequenceTo)
    {
        if (sequenceTo <= 0)
        {
            ActivePlanNode = null;
            return;
        }

        var source = FilteredEvents.Count > 0 ? FilteredEvents : Events;

        var identifier = source
            .Where(e => e.SequenceId <= sequenceTo && e.PlanNodeIdentifier is not null)
            .OrderByDescending(e => e.SequenceId)
            .Select(e => e.PlanNodeIdentifier)
            .FirstOrDefault();

        ActivePlanNode = identifier is null ? null : ResolvePlanNode(identifier);
    }

    private PlanNode? ResolvePlanNode(PlanNodeIdentifier identifier)
    {
        var plan = ExecutionPlans.FirstOrDefault(p => p.PlanHandle == identifier.PlanHandle)
                   ?? ExecutionPlans.FirstOrDefault();

        return plan is not null && plan.NodesById.TryGetValue(identifier.NodeId, out var node) ? node : null;
    }

    /// <summary>Selects the plan node for the given identifier (e.g. clicked in the timeline).</summary>
    public void SelectPlanNode(PlanNodeIdentifier identifier)
    {
        ActivePlanNode = ResolvePlanNode(identifier);
    }

    [ObservableProperty]
    private bool includeSystemObjects;

    partial void OnIncludeSystemObjectsChanged(bool value) => RefreshFilteredEvents();

    public Visibility HasEvents
        => Events.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    
    private List<AllocationLayer> ObjectLayers { get; set; }

    public QueryViewModel(ILogger<QueryViewModel> logger,
                          QueryRunner queryRunner,
                          SettingsService settingsService,
                          DatabaseSource database)
    {
        Logger = logger;
        QueryRunner = queryRunner;
        SettingsService = settingsService;
        Database = database;
        Message = string.Empty;

        Name = "Query";

        DatabaseFiles = database.Files
            .Select(f => new DatabaseFile(this) { FileId = f.FileId, Size = f.Size })
            .ToArray();

        ObjectLayers = AllocationLayerBuilder.GenerateLayers(database, true, true);

        ExtentCount = database.GetFileSize(1) / 8;

        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);

        PfsChain = database.Pfs.First().Value;

        systemObjectIds = database.AllocationUnits
            .Where(u => u.IsSystem)
            .Select(u => u.ObjectId)
            .ToHashSet();

        var sqlDocument = new DocumentViewModel(DockDocumentKind.Sql, "SQL", this, canClose: false);
        var allocationDocument = new DocumentViewModel(DockDocumentKind.Allocation, "Allocations", this, canClose: false);
        var planDocument = new DocumentViewModel(DockDocumentKind.Plan, "Execution Plan", this);
        EventsDocument = new DocumentViewModel(DockDocumentKind.Events, "Events", this);

        Dock = new DockLayoutViewModel(new TabGroupNode(sqlDocument, allocationDocument, planDocument, EventsDocument));
    }

    [RelayCommand]
    private async Task OpenIndexView()
    {
        //if (Page is AllocationUnitPage allocationUnitPage)
        //{
        //    var rootPage = allocationUnitPage.AllocationUnit.RootPage;

        //    await WeakReferenceMessenger.Default.Send(new OpenIndexMessage(new OpenIndexRequest(Database, rootPage)));
        //}
    }

    [RelayCommand]
    private async Task ExecuteQuery(ExecuteSqlPayload payload)
    {
        ClearResults();

        var results = await QueryRunner.TraceQuery(payload.SqlText,
                                                   Database,
                                                   payload.QueryOptions.ClearBufferPool,
                                                   payload.QueryOptions.DisableReadAhead,
                                                   payload.StatementType == StatementType.Modification);

        if (!results.IsSuccess)
        {
            IsError = true;
            Message = results.Message;
            return;
        }

        if (cropToQuery)
        {
            var queryNode = results.EngineEvents.FirstOrDefault(e =>
                e is ExecutionOperatorEvent { PlanNodeIdentifier.NodeId: -1 });

            if (queryNode != null)
            {
                StartTime = queryNode.TimeMs;
                EndTime = queryNode.TimeMs + queryNode.Duration;
            }
        }
        else
        {
            StartTime = 0;
            EndTime = events.DefaultIfEmpty().Max(e => e?.TimeMs + e?.Duration);
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

        EventColourProvider.SetEventColours(results.ExecutionPlans, results.EngineEvents);

        DispatcherQueue.TryEnqueue(async void () =>
        {
            Events = results.EngineEvents;

            ExecutionPlans = new ObservableCollection<ExecutionPlan>(results.ExecutionPlans);

            await BuildFilterTreeAsync();
        });

        RefreshLayers(results.EngineEvents);
    }

    private void ClearResults()
    {
        IsError = false;
        Message = string.Empty;

        SequenceFrom = 0;
        SequenceTo = 0;
        PlayheadSequence = 0;
        IsTimelinePlaying = false;
        ActivePlanNode = null;

        Events = [];
        FilteredEvents = [];
        ExecutionPlans = [];
        FilterNodes = [];

        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);
        SelectedLayers = [];
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

    private bool _filterRefreshPending;
    private bool _saveUncheckedPending;

    private void OnFilterNodeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_filterRefreshPending)
        {
            _filterRefreshPending = true;
            DispatcherQueue.TryEnqueue(() =>
            {
                _filterRefreshPending = false;
                RefreshFilteredEvents();
            });
        }

        if (!_saveUncheckedPending)
        {
            _saveUncheckedPending = true;
            DispatcherQueue.TryEnqueue(async () =>
            {
                _saveUncheckedPending = false;
                await SaveUncheckedAsync();
            });
        }
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

        foreach (var eventNode in root.Children.Where(n => n is { IsChecked: false, Children.Count: 0 }))
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

        var eventNodesByName = root.Children.ToDictionary(n => n.Label, StringComparer.OrdinalIgnoreCase);

        return source.Where(e =>
        {
            if (!eventNodesByName.TryGetValue(e.Name, out var eventNode))
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
        var lockLayer = new AllocationLayer { Name = "Lock", Colour = Color.Gray, IsVisible = true };

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
                            systemIoLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId)
                            { DisplayColour = e.DisplayColour });
                        }
                        else
                        {
                            ioLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId, e.SequenceId)
                            { DisplayColour = e.DisplayColour });
                        }

                        break;
                    case LockEvent lockEvent:
                        if (isSystemObject)
                        {
                            systemLockLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId)
                            { DisplayColour = e.DisplayColour });
                        }
                        else
                        {
                            lockLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId, e.SequenceId)
                            { DisplayColour = e.DisplayColour });
                        }

                        break;
                }
            }
        }

        return [ioLayer, pageLayer, lockLayer, systemIoLayer, systemPageLayer, systemLockLayer];
    }
}
