using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Query;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Plans;
using InternalsViewer.UI.App.Controls.SqlEditor;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Schema;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.ViewModels.Tabs;
using InternalsViewer.UI.App.Views.Query.Tabs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DatabaseFile = InternalsViewer.UI.App.Models.DatabaseFile;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed class QueryViewModelFactory(ILogger<QueryViewModel> logger,
                                          QueryRunner queryRunner,
                                          SettingsService settingsService,
                                          IndexTabViewModelFactory indexTabViewModelFactory)
{
    public QueryViewModel Create(DatabaseSource database) => new(logger,
                                                                 queryRunner,
                                                                 settingsService,
                                                                 indexTabViewModelFactory,
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
    private bool _isTooltipEnabled = true;

    [ObservableProperty]
    private bool _isTimelinePlaying;

    [ObservableProperty]
    private bool _includeLocks = false;

    [ObservableProperty]
    private bool _includeIo = false;

    [ObservableProperty]
    private EventOptions _eventOptions = new();

    [ObservableProperty]
    private int _extentCount;

    [ObservableProperty]
    private bool _cropToQuery = true;

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
    private EventColourProvider _eventColours = new([]);

    [ObservableProperty]
    private HashSet<int> _systemObjectIds;

    [ObservableProperty]
    private long _sequenceFrom;

    [ObservableProperty]
    private long _sequenceTo;

    [ObservableProperty]
    private long? _startOffset;

    [ObservableProperty]
    private long? _endOffset;

    private long _playheadTimeUs;
    private long _scopeFromUs;

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
    private bool _isCallstackVisible;

    [ObservableProperty]
    private bool _isEventSelectionPanelOpen;

    [ObservableProperty]
    private List<CallstackFrame> _callstacks = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCallstack))]
    private EngineEvent? _selectedEvent;

    public List<CallstackFrame> SelectedCallstack => SelectedEvent?.Callstack ?? [];

    partial void OnIsSqlEditorVisibleChanged(bool value) => SetDocumentVisible(SqlDocument, value);

    partial void OnIsAllocationsVisibleChanged(bool value) => SetDocumentVisible(AllocationsDocument, value);

    partial void OnIsExecutionPlanVisibleChanged(bool value) => SetDocumentVisible(PlanDocument, value);

    partial void OnIsEventsVisibleChanged(bool value) => SetDocumentVisible(EventsDocument, value);

    partial void OnIsCallstackVisibleChanged(bool value) => SetDocumentVisible(CallstackDocument, value);

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
        IsCallstackVisible = Dock.Contains(CallstackDocument);

        _suppressVisibilitySync = false;
    }

    public DockLayoutViewModel Dock { get; }

    private DocumentViewModel SqlDocument { get; }

    private DocumentViewModel AllocationsDocument { get; }

    private DocumentViewModel PlanDocument { get; }

    private DocumentViewModel EventsDocument { get; }

    private DocumentViewModel CallstackDocument { get; }

    private Dictionary<string, DocumentViewModel> DocumentsByKey { get; }

    public event Action<EngineEvent>? EventNavigationRequested;

    public void NavigateToEvent(EngineEvent engineEvent)
    {
        SelectedEvent = engineEvent;

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
        PruneClosedIndexes();
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
            SettingsOpen = IsEventSelectionPanelOpen,
            IncludeLock = EventOptions.IncludeLock,
            IncludeWait = EventOptions.IncludeWait,
            IncludeCallstack = EventOptions.IncludeCallstack
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
        EventOptions = new EventOptions
        {
            IncludeLock = dto.IncludeLock,
            IncludeWait = dto.IncludeWait,
            IncludeCallstack = dto.IncludeCallstack
        };

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
    private PlanNode? _selectedPlanNode;

    [ObservableProperty]
    private IReadOnlyList<PlanNode> _activePlanNodes = [];

    [ObservableProperty]
    private IReadOnlyList<PlanNode> _emittingPlanNodes = [];

    public void SetScope(long fromUs, long toUs)
    {
        _scopeFromUs = fromUs;

        var source = FilteredEvents.Count > 0 ? FilteredEvents : Events;

        var from = long.MaxValue;
        var to = long.MinValue;

        foreach (var e in source)
        {
            // Point events define the sequence scope (operator events carry offset sequence ids).
            if (e is ExecutionOperatorEvent || e.TimeUs < fromUs || e.TimeUs > toUs)
            {
                continue;
            }

            if (e.SequenceId < from) from = e.SequenceId;
            if (e.SequenceId > to) to = e.SequenceId;
        }

        SequenceFrom = from <= to ? from : 0;
        SequenceTo = from <= to ? to : 0;
    }

    public void SetPlayheadTime(long timeUs)
    {
        _playheadTimeUs = timeUs;

        UpdateActiveOperators(timeUs);
        SyncIndexPage(timeUs);
    }

    /// <summary>
    /// Derives the active operators from the playhead time: every operator whose run-time span contains
    /// the playhead, and the subset of those that have started emitting rows (past their consume phase).
    /// This reflects the operators running concurrently at the position - a parallel query has several -
    /// and which are producing rows, so the plan can show the data flow.
    /// </summary>
    private void UpdateActiveOperators(long timeUs)
    {
        var source = FilteredEvents.Count > 0 ? FilteredEvents : Events;

        // At the very start of the timeline nothing has run yet - leave the plan in its default (grey)
        // state rather than lighting up operators that merely start at time zero.
        if (timeUs <= AxisStartUs(source))
        {
            if (ActivePlanNodes.Count > 0)
            {
                ActivePlanNodes = [];
            }

            if (EmittingPlanNodes.Count > 0)
            {
                EmittingPlanNodes = [];
            }

            return;
        }

        var active = new List<PlanNode>();
        var emitting = new List<PlanNode>();
        var seen = new HashSet<PlanNode>();

        foreach (var op in source.OfType<ExecutionOperatorEvent>())
        {
            if (op.TimeUs > timeUs || timeUs > op.TimeUs + op.DurationUs)
            {
                continue;
            }

            if (op.PlanNodeIdentifier is not { } id || ResolvePlanNode(id) is not { } node || !seen.Add(node))
            {
                continue;
            }

            active.Add(node);

            if (op.EmitStartUs <= timeUs)
            {
                emitting.Add(node);
            }
        }

        if (!ActivePlanNodes.SequenceEqual(active))
        {
            ActivePlanNodes = active;
        }

        if (!EmittingPlanNodes.SequenceEqual(emitting))
        {
            EmittingPlanNodes = emitting;
        }
    }

    private long AxisStartUs(IReadOnlyList<EngineEvent> source)
    {
        if (StartOffset is { } start)
        {
            return start;
        }

        var min = long.MaxValue;

        foreach (var e in source)
        {
            if (e.TimeUs < min)
            {
                min = e.TimeUs;
            }
        }

        return min == long.MaxValue ? 0 : min;
    }

    private PlanNode? ResolvePlanNode(PlanNodeIdentifier identifier)
    {
        var plan = ExecutionPlans.FirstOrDefault(p => p.PlanHandleId == identifier.PlanHandleId)
                   ?? ExecutionPlans.FirstOrDefault();

        return plan is not null && plan.NodesById.TryGetValue(identifier.NodeId, out var node) ? node : null;
    }

    public void SelectPlanNode(PlanNodeIdentifier identifier)
    {
        SelectedPlanNode = ResolvePlanNode(identifier);
    }

    private readonly Dictionary<string, IndexTabViewModel> _openIndexes = new();

    public void OpenIndex(ExecutionOperatorEvent op)
        => OpenIndex(op.SchemaName, op.TableName, op.IndexName);

    public void OpenIndex(PlanNode node)
        => OpenIndex(node.Schema, node.Table, node.Index);

    private void OpenIndex(string? schema, string? table, string? index)
    {
        if (string.IsNullOrEmpty(index))
        {
            return;
        }

        schema ??= string.Empty;
        table ??= string.Empty;

        var key = $"Index:{schema}.{table}.{index}";

        if (DocumentsByKey.TryGetValue(key, out var existing))
        {
            Dock.Show(existing);
            return;
        }

        var allocationUnit = Database.AllocationUnits
                                     .Values
                                     .FirstOrDefault(a => NameMatches(a.IndexName, index)
                                                          && NameMatches(a.TableName, table)
                                                          && (schema.Length == 0 || NameMatches(a.SchemaName, schema))
                                                          && a.AllocationUnitType == AllocationUnitType.InRowData);

        if (allocationUnit is null)
        {
            Logger.LogWarning("Index not found: {Schema}.{Table}.{Index}", schema, table, index);

            return;
        }

        var indexViewModel = _indexTabViewModelFactory.Create(Database);

        indexViewModel.RootPage = allocationUnit.RootPage;

        var document = new DocumentViewModel(title: $"Index: {index}",
                                             content: indexViewModel,
                                             viewFactory: static () => new IndexDocumentView(),
                                             canClose: true,
                                             keepAlive: true,
                                             key: key,
                                             persist: false);

        DocumentsByKey[key] = document;
        _openIndexes[key] = indexViewModel;

        Dock.Show(document);

        // Reflect the current playhead position immediately.
        SyncIndexPage(_playheadTimeUs);
    }

    /// <summary>
    /// Drops index tabs the user has closed (they are transient and recreated on demand)
    /// </summary>
    private void PruneClosedIndexes()
    {
        if (_openIndexes.Count == 0)
        {
            return;
        }

        var closed = _openIndexes.Keys
            .Where(k => !(DocumentsByKey.TryGetValue(k, out var d) && Dock.Contains(d)))
            .ToList();

        foreach (var key in closed)
        {
            _openIndexes.Remove(key);

            if (DocumentsByKey.Remove(key, out var document))
            {
                document.DisposeView();
            }
        }
    }

    // Caches each page's owning index root page, so the per-playhead range scan doesn't repeatedly
    // resolve the same pages' allocation units.
    private readonly Dictionary<PageAddress, PageAddress?> _pageRootCache = new();

    private PageAddress? RootPageOf(PageAddress page)
    {
        if (!_pageRootCache.TryGetValue(page, out var root))
        {
            root = Database.FindPageAllocationUnit(page)?.RootPage;
            _pageRootCache[page] = root;
        }

        return root;
    }

    /// <summary>
    /// Updates each open index tab from the playhead time: the single page actively being read at the
    /// playhead, plus the range of every page the index read between the clip start and the playhead.
    /// Working in time (not sequence) means reads from different operators at the same coarse timestamp
    /// are resolved by where they actually sit, so each operator's index tracks correctly.
    /// </summary>
    private void SyncIndexPage(long playheadUs)
    {
        if (_openIndexes.Count == 0)
        {
            return;
        }

        var source = FilteredEvents.Count > 0 ? FilteredEvents : Events;

        // The active read: the latest page read at or before the playhead (by time).
        PageAddress? activePage = null;

        var activeReadTime = long.MinValue;

        // The range: every page read between the clip start and the playhead, grouped by index root page.
        var rangeByRoot = new Dictionary<PageAddress, List<PageAddress>>();

        foreach (var e in source)
        {
            if (e is not IoEvent { IsRead: true } || e.PageAddress is not { } pg)
            {
                continue;
            }

            if (e.TimeUs <= playheadUs && e.TimeUs > activeReadTime)
            {
                activeReadTime = e.TimeUs;
                activePage = pg;
            }

            if (e.TimeUs >= _scopeFromUs && e.TimeUs <= playheadUs && RootPageOf(pg) is { } root)
            {
                if (!rangeByRoot.TryGetValue(root, out var pages))
                {
                    pages = [];
                    rangeByRoot[root] = pages;
                }

                pages.Add(pg);
            }
        }

        var activeRoot = activePage is { } ap ? RootPageOf(ap) : null;

        foreach (var viewModel in _openIndexes.Values)
        {
            viewModel.SelectedPageAddress = activeRoot is not null && viewModel.RootPage == activeRoot
                ? activePage
                : null;

            viewModel.SelectedPageAddresses = rangeByRoot.TryGetValue(viewModel.RootPage, out var range)
                ? range
                : [];
        }
    }

    private static bool NameMatches(string? a, string? b) =>
        string.Equals(a?.Trim('[', ']'), b?.Trim('[', ']'), StringComparison.OrdinalIgnoreCase);

    public Visibility HasEvents
        => Events.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    private List<AllocationLayer> ObjectLayers { get; set; }

    public QueryViewModel(ILogger<QueryViewModel> logger,
                          QueryRunner queryRunner,
                          SettingsService settingsService,
                          IndexTabViewModelFactory indexTabViewModelFactory,
                          DatabaseSource database)
    {
        Logger = logger;
        QueryRunner = queryRunner;
        Database = database;
        _settingsService = settingsService;
        _indexTabViewModelFactory = indexTabViewModelFactory;
        Message = string.Empty;

        Name = $"{Database.Name}: Query";

        DatabaseFiles = database.Files
            .Select(f => new DatabaseFile(this) { FileId = f.FileId, Size = f.Size })
            .ToArray();

        ObjectLayers = AllocationLayerBuilder.GenerateLayers(database, true, true);

        ExtentCount = database.GetFilePageCount(1) / 8;

        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);

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
        CallstackDocument = DocumentViewModel.Create<CallstackDocumentView>("Callstack", this, keepAlive: true, key: "Callstack");

        DocumentsByKey = new Dictionary<string, DocumentViewModel>
        {
            [SqlDocument.Key] = SqlDocument,
            [AllocationsDocument.Key] = AllocationsDocument,
            [PlanDocument.Key] = PlanDocument,
            [EventsDocument.Key] = EventsDocument,
            [CallstackDocument.Key] = CallstackDocument,
        };

        Dock = new DockLayoutViewModel(new TabGroupNode(SqlDocument));

        Dock.LayoutChanged += OnDockLayoutChanged;

        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await RestoreLayoutAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error restoring layout");
            }
        });
    }

    private readonly SettingsService _settingsService;

    private readonly IndexTabViewModelFactory _indexTabViewModelFactory;

    // Padding (microseconds) added either side of the query crop so boundary reads aren't clipped.
    private const long CropPaddingUs = 200;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ExecuteQuery(ExecuteSqlPayload payload, CancellationToken cancellationToken)
    {
        EventOptions = payload.EventOptions;
        ScheduleSaveLayout();

        ClearResults();

        var progress = new Progress<string>(message => Message = string.IsNullOrEmpty(Message)
                                                                 ? message
                                                                 : Message + Environment.NewLine + message);

        // Run full trace on background thread
        var (results, layers, colours) =
            await Task.Run(async () =>
            {
                var queryResult = await QueryRunner.TraceQuery(payload.SqlText,
                                                               Database,
                                                               payload.QueryOptions.ClearBufferPool,
                                                               payload.QueryOptions.DisableReadAhead,
                                                               payload.StatementType == StatementType.Modification,
                                                               payload.EventOptions,
                                                               progress,
                                                               cancellationToken);

                if (!queryResult.IsSuccess)
                {
                    return (queryResult, new List<AllocationLayer>(), new EventColourProvider([]));
                }

                var names = Database.AllocationUnits
                                    .Values
                                    .GroupBy(u => u.ObjectId)
                                    .ToDictionary(g => g.Key, g => g.First().DisplayName);

                foreach (var e in queryResult.EngineEvents.Where(e => e.ObjectId > 0))
                {
                    e.ObjectName = names.TryGetValue(e.ObjectId, out var n) ? n : $"(Object Id: {e.ObjectId})";
                }

                var colourProvider = new EventColourProvider(queryResult.ExecutionPlans);

                var allocationLayer = GetEventsAllocationLayer(queryResult.EngineEvents, colourProvider);

                return (queryResult, allocationLayer, colourProvider);
            },
            cancellationToken);

        if (!results.IsSuccess)
        {
            IsError = true;
            Message = Message + Environment.NewLine + results.Message;
            return;
        }

        if (CropToQuery)
        {
            var queryNode = results.EngineEvents.FirstOrDefault(e =>
                e is ExecutionOperatorEvent { PlanNodeIdentifier.NodeId: -1 });

            if (queryNode != null)
            {
                StartOffset = Math.Max(0, queryNode.TimeUs - CropPaddingUs);
                EndOffset = queryNode.TimeUs + queryNode.DurationUs + CropPaddingUs;
            }
        }
        else
        {
            // No crop - show the full captured range.
            StartOffset = null;
            EndOffset = null;
        }

        IsError = false;
        Message = Message + Environment.NewLine + $"({results.RowCount} rows affected)";

        try
        {
            // Publish the colour provider before the events so the timeline has it when it (re)paints.
            EventColours = colours;

            Events = results.EngineEvents;

            Callstacks = results.EngineEvents
                                .Where(e => e.Callstack is { Count: > 0 })
                                .SelectMany(e => e.Callstack!)
                                .ToList();

            ExecutionPlans = new ObservableCollection<ExecutionPlan>(results.ExecutionPlans);

            await EventFilter.BuildAsync(Events);

            ShowResultTabsForFirstRun();

            ApplyEventLayers(layers);
        }
        catch (Exception ex)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(ex));
        }
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
        _playheadTimeUs = 0;
        _scopeFromUs = 0;
        IsTimelinePlaying = false;
        SelectedPlanNode = null;
        ActivePlanNodes = [];
        EmittingPlanNodes = [];

        Events = [];
        FilteredEvents = [];
        Callstacks = [];
        SelectedEvent = null;
        ExecutionPlans = [];

        foreach (var indexViewModel in _openIndexes.Values)
        {
            indexViewModel.SelectedPageAddress = null;
            indexViewModel.SelectedPageAddresses = [];
        }

        EventFilter.Clear();

        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);
        SelectedLayers = [];
    }

    private void RefreshLayers(List<EngineEvent> engineEvents)
        => ApplyEventLayers(GetEventsAllocationLayer(engineEvents, EventColours));

    // Applies the (already computed) event layers. Must run on the UI thread - it assigns bound
    // collections.
    private void ApplyEventLayers(List<AllocationLayer> layers)
    {
        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);

        foreach (var layer in layers)
        {
            AllocationLayers.Add(layer);
        }

        SelectedLayers = new ObservableCollection<AllocationLayer>(layers);
    }

    public void RefreshFilteredEvents()
    {
        FilteredEvents = [.. EventFilter.Apply(Events)];

        RefreshLayers(FilteredEvents);
    }

    private List<AllocationLayer> GetEventsAllocationLayer(List<EngineEvent> engineEvents,
                                                          EventColourProvider colours)
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
                            { DisplayColour = colours.GetColour(e) });
                        }
                        else
                        {
                            ioLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId, e.SequenceId)
                            { DisplayColour = colours.GetColour(e) });
                        }

                        break;
                    case LockEvent lockEvent:
                        if (isSystemObject)
                        {
                            systemLockLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId)
                            { DisplayColour = colours.GetColour(e) });
                        }
                        else
                        {
                            lockLayer.PageSpans.Add(new PageSpan(e.PageAddress.Value, e.SequenceId, e.SequenceId)
                            { DisplayColour = colours.GetColour(e) });
                        }

                        break;
                }
            }
        }

        return [ioLayer, pageLayer, lockLayer, systemIoLayer, systemPageLayer, systemLockLayer];
    }

    public PfsChain PfsChain => new();
}
