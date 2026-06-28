using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Controls.SqlEditor;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Schema;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Tabs;
using InternalsViewer.UI.App.Views.Query.Tabs;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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
    public ILogger<QueryViewModel> Logger { get; }

    public QueryRunner QueryRunner { get; }

    public DatabaseSource Database { get; }

    public EventFilterViewModel EventFilter { get; }

    [ObservableProperty] 
    private bool _isError;

    [ObservableProperty] 
    private string _message;

    [ObservableProperty]
    private string _sql = string.Empty;

    [ObservableProperty]
    private bool _isPfsVisible = false;

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _selectedLayers = [];

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _allocationLayers = [];

    [ObservableProperty]
    private PfsChain _pfsChain = new();

    [ObservableProperty]
    private bool _isTooltipEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorVisibility))]
    private bool _isTimelinePlaying;

    [ObservableProperty]
    private bool _includeLocks = false;

    [ObservableProperty]
    private bool _includeIo = false;

    [ObservableProperty]
    private int _extentCount;

    [ObservableProperty]
    public bool _cropToQuery = true;

    [ObservableProperty]
    private double _allocationMapHeight = 200;

    [ObservableProperty]
    private DatabaseFile[] _databaseFiles = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEvents))]
    private List<EngineEvent> _events = [];

    [ObservableProperty]
    private List<EngineEvent> _filteredEvents = [];

    [ObservableProperty]
    private HashSet<int> _systemObjectIds;

    [ObservableProperty]
    private long _sequenceFrom;

    [ObservableProperty]
    private long _sequenceTo;

    [ObservableProperty]
    public double? _startTime;

    [ObservableProperty]
    public double? _endTime;

    [ObservableProperty]
    private long _playheadSequence;

    [ObservableProperty]
    private DatabaseSchema? _schema;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimelineRowHeight))]
    [NotifyPropertyChangedFor(nameof(TimelineSplitterVisibility))]
    private bool _isTimelineVisible = true;

    public GridLength TimelineRowHeight
        => IsTimelineVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public Visibility TimelineSplitterVisibility
        => IsTimelineVisible ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private bool _isSqlEditorVisible = true;

    [ObservableProperty]
    private bool _isAllocationsVisible;

    [ObservableProperty]
    private bool _isExecutionPlanVisible;

    [ObservableProperty]
    private bool _isEventsVisible;

    [ObservableProperty]
    private bool _isEventSelectionPanelOpen;

    partial void OnIsSqlEditorVisibleChanged(bool value) => SetDocumentVisible(SqlDocument, value);

    partial void OnIsAllocationsVisibleChanged(bool value) => SetDocumentVisible(AllocationsDocument, value);

    partial void OnIsExecutionPlanVisibleChanged(bool value) => SetDocumentVisible(PlanDocument, value);

    partial void OnIsEventsVisibleChanged(bool value) => SetDocumentVisible(EventsDocument, value);

    partial void OnIsTimelineVisibleChanged(bool value) => ScheduleSaveLayout();

    partial void OnIsEventSelectionPanelOpenChanged(bool value) => ScheduleSaveLayout();

    private void SetDocumentVisible(DocumentViewModel document, bool show)
    {
        if (_suppressVisibilitySync)
        {
            return;
        }

        if (show)
        {
            Dock.Show(document);
        }
        else
        {
            Dock.Close(document);
        }
    }

    private void SyncTabVisibility()
    {
        _suppressVisibilitySync = true;

        IsSqlEditorVisible = Dock.Contains(SqlDocument);
        IsAllocationsVisible = Dock.Contains(AllocationsDocument);
        IsExecutionPlanVisible = Dock.Contains(PlanDocument);
        IsEventsVisible = Dock.Contains(EventsDocument);

        _suppressVisibilitySync = false;
    }

    public DockLayoutViewModel Dock { get; }

    private DocumentViewModel SqlDocument { get; }

    private DocumentViewModel AllocationsDocument { get; }

    private DocumentViewModel PlanDocument { get; }

    private DocumentViewModel EventsDocument { get; }

    private Dictionary<string, DocumentViewModel> DocumentsByKey { get; }

    public event Action<EngineEvent>? EventNavigationRequested;

    public void NavigateToEvent(EngineEvent engineEvent)
    {
        IsEventsVisible = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            Dock.Activate(EventsDocument);
            EventNavigationRequested?.Invoke(engineEvent);
        });
    }

    private const string LayoutSettingKey = "QueryDockLayout";

    private bool _suppressVisibilitySync;
    private bool _isRestoringLayout;
    private bool _layoutRestored;
    private bool _saveScheduled;

    private void OnDockLayoutChanged(object? sender, EventArgs e)
    {
        SyncTabVisibility();
        ScheduleSaveLayout();
    }

    private void ScheduleSaveLayout()
    {
        if (_isRestoringLayout || _suppressVisibilitySync || _saveScheduled)
        {
            return;
        }

        _saveScheduled = true;

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        DispatcherQueue.TryEnqueue(async void () =>
        {
            _saveScheduled = false;

            try
            {
                await SaveLayoutAsync();
            }
            catch (Exception e)
            {
                Logger.LogError("Error saving layout - {Message}", e.Message);
            }
        });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
    }

    public async Task SaveLayoutAsync()
    {
        var dto = new QueryLayoutState
        {
            Root = DockLayoutSerializer.Serialize(Dock.Root),
            TimelineVisible = IsTimelineVisible,
            SettingsOpen = IsEventSelectionPanelOpen
        };

        await _settingsService.SaveSettingAsync(LayoutSettingKey, dto);
    }

    private async Task RestoreLayoutAsync()
    {
        var dto = await _settingsService.ReadSettingAsync<QueryLayoutState>(LayoutSettingKey);

        var root = DockLayoutSerializer.Deserialize(dto?.Root, key => DocumentsByKey.GetValueOrDefault(key));

        if (dto is null || root is null)
        {
            return;
        }

        _isRestoringLayout = true;

        IsTimelineVisible = dto.TimelineVisible;
        IsEventSelectionPanelOpen = dto.SettingsOpen;

        Dock.SetRoot(root);

        _layoutRestored = true;
        _isRestoringLayout = false;

        SyncTabVisibility();
    }

    [RelayCommand]
    private void ResetLayout()
    {
        _layoutRestored = false;
        _resultTabsOpened = false;

        IsTimelineVisible = true;
        IsEventSelectionPanelOpen = false;

        Dock.SetRoot(new TabGroupNode(SqlDocument));
    }

    [ObservableProperty]
    private ObservableCollection<ExecutionPlan> _executionPlans = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorName))]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorObject))]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorIcon))]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorVisibility))]
    private PlanNode? _activePlanNode;

    partial void OnPlayheadSequenceChanged(long value) 
        => UpdateActiveOperator(value);

    public string ActiveOperatorName 
        => ActivePlanNode?.PhysicalOperator ?? string.Empty;

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

    private void UpdateActiveOperator(long operatorSequenceTo)
    {
        if (operatorSequenceTo <= 0)
        {
            ActivePlanNode = null;
            return;
        }

        var source = FilteredEvents.Count > 0 ? FilteredEvents : Events;

        var identifier = source
            .Where(e => e.SequenceId <= operatorSequenceTo && e.PlanNodeIdentifier is not null)
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

    public void SelectPlanNode(PlanNodeIdentifier identifier)
    {
        ActivePlanNode = ResolvePlanNode(identifier);
    }

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
        Database = database;
        _settingsService = settingsService;
        Message = string.Empty;

        Name = $"{Database.Name}: Query";

        DatabaseFiles = database.Files
            .Select(f => new DatabaseFile(this) { FileId = f.FileId, Size = f.Size })
            .ToArray();

        ObjectLayers = AllocationLayerBuilder.GenerateLayers(database, true, true);

        ExtentCount = database.GetFilePageCount(1) / 8;

        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);

        PfsChain = database.Pfs.First().Value;

        _systemObjectIds = database.AllocationUnits
                                   .Values
                                   .Where(u => u.IsSystem)
                                   .Select(u => u.ObjectId)
                                   .ToHashSet();

        EventFilter = new EventFilterViewModel(settingsService);
        EventFilter.SetSystemObjectIds(_systemObjectIds);
        EventFilter.FilterChanged += RefreshFilteredEvents;

        Schema = SchemaHelper.ToSqlSchema(database);

        SqlDocument = DocumentViewModel.Create<SqlDocumentView>("SQL", this, keepAlive: true, key: "Sql");
        AllocationsDocument = DocumentViewModel.Create<AllocationDocumentView>("Allocations", this, keepAlive: true, key: "Allocations");
        PlanDocument = DocumentViewModel.Create<PlanDocumentView>("Execution Plan", this, keepAlive: true, key: "Plan");
        EventsDocument = DocumentViewModel.Create<EventsDocumentView>("Events", this, keepAlive: true, key: "Events");

        DocumentsByKey = new Dictionary<string, DocumentViewModel>
        {
            [SqlDocument.Key] = SqlDocument,
            [AllocationsDocument.Key] = AllocationsDocument,
            [PlanDocument.Key] = PlanDocument,
            [EventsDocument.Key] = EventsDocument,
        };

        Dock = new DockLayoutViewModel(new TabGroupNode(SqlDocument));
        Dock.LayoutChanged += OnDockLayoutChanged;

        DispatcherQueue.TryEnqueue(async () => await RestoreLayoutAsync());
    }

    private readonly SettingsService _settingsService;

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

        if (CropToQuery)
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
            EndTime = Events.DefaultIfEmpty().Max(e => e?.TimeMs + e?.Duration);
        }

        IsError = false;
        Message = $"({results.RowCount} rows affected)";

        var names = Database.AllocationUnits
                            .Values
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

            await EventFilter.BuildAsync(Events);

            ShowResultTabsForFirstRun();
        });

        RefreshLayers(results.EngineEvents);
    }

    private bool _resultTabsOpened;

    private void ShowResultTabsForFirstRun()
    {
        if (_layoutRestored || _resultTabsOpened)
        {
            return;
        }

        _resultTabsOpened = true;

        IsAllocationsVisible = true;
        IsExecutionPlanVisible = true;
        IsEventsVisible = true;
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

        EventFilter.Clear();

        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);
        SelectedLayers = [];
    }

    private void RefreshLayers(List<EngineEvent> engineEvents)
    {
        var layers = GetEventsAllocationLayer(engineEvents);

        DispatcherQueue.TryEnqueue(() =>
        {
            AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);

            foreach (var layer in layers)
            {
                AllocationLayers.Add(layer);
            }

            SelectedLayers = new ObservableCollection<AllocationLayer>(layers);
        });
    }

    public void RefreshFilteredEvents()
    {
        FilteredEvents = [.. EventFilter.Apply(Events)];

        RefreshLayers(FilteredEvents);
    }

    private List<AllocationLayer> GetEventsAllocationLayer(List<EngineEvent> engineEvents)
    {
        var maxFileId = DatabaseFiles.Max(d => d.FileId);

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
