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
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Results;
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
using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Callstack.Categories;
using DatabaseFile = InternalsViewer.UI.App.Models.DatabaseFile;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed class QueryViewModelFactory(ILogger<QueryViewModel> logger,
                                          QueryRunner queryRunner,
                                          SettingsService settingsService,
                                          SettingsViewModel settingsViewModel,
                                          IndexTabViewModelFactory indexTabViewModelFactory)
{
    public QueryViewModel Create(DatabaseSource database) => new(logger,
                                                                 queryRunner,
                                                                 settingsService,
                                                                 settingsViewModel,
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
    private ObservableCollection<AllocationLayer> _allocationLayers = [];

    [ObservableProperty]
    private bool _isTooltipEnabled = true;

    private bool _autoScroll = true;

    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    private bool _isHeatmap;

    public bool IsHeatmap
    {
        get => _isHeatmap;
        set => SetProperty(ref _isHeatmap, value);
    }

    [ObservableProperty]
    private bool _isTimelinePlaying;

    [ObservableProperty]
    private EventOptions _eventOptions = new();

    // Lock/latch/wait are always captured (the read grouping needs them); these decide only whether they show on the
    // timeline. Mirrored into EventOptions so the saved layout persists the choice.
    [ObservableProperty]
    private bool _showLocks = true;

    [ObservableProperty]
    private bool _showLatches = true;

    [ObservableProperty]
    private bool _showWaits = true;

    partial void OnShowLocksChanged(bool value) => EventOptions.IncludeLock = value;

    partial void OnShowLatchesChanged(bool value) => EventOptions.IncludeLatch = value;

    partial void OnShowWaitsChanged(bool value) => EventOptions.IncludeWait = value;

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

    [ObservableProperty]
    private long _playheadTimeUs;

    private long _scopeFromUs;

    [ObservableProperty]
    private DatabaseSchema? _schema;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimelineRowHeight))]
    [NotifyPropertyChangedFor(nameof(SplitterVisibility))]
    private bool _isTimelineVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailsRowHeight))]
    [NotifyPropertyChangedFor(nameof(SplitterVisibility))]
    private bool _isDetailsVisible = true;

    public GridLength TimelineRowHeight
        => IsTimelineVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public GridLength DetailsRowHeight
        => IsDetailsVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public Visibility SplitterVisibility
        => (IsTimelineVisible && IsDetailsVisible) ? Visibility.Visible : Visibility.Collapsed;

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
    [NotifyPropertyChangedFor(nameof(ActiveResultSet))]
    private List<QueryResultSet> _resultSets = [];

    public QueryResultSet? ActiveResultSet => ResultSets.Count > 0 ? ResultSets[0] : null;

    [ObservableProperty]
    private List<CallstackFrame> _callstacks = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CallStackRoots))]
    private CallStackTree? _callStack;

    /// <summary>
    /// The top-level frames (thread starts) of the query's merged call stack tree, for the call tree view
    /// </summary>
    public IEnumerable<CallStackNode> CallStackRoots => CallStack?.Root.ChildNodes ?? [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCallstack))]
    private EngineEvent? _selectedEvent;

    // The selected event's path is recovered by walking its leaf node up the shared call stack tree.
    public List<CallstackFrame> SelectedCallstack
        => SelectedEvent?.CallStack?.Path()
                        .Where(f => f.Resolved is null
                                 || (f.Resolved.ModuleCategory.GetCategoryMetadata()?.IsInfrastructure != true
                                  && f.Resolved.SymbolCategory.GetCategoryMetadata()?.IsInfrastructure != true))
                        .ToList() ?? [];

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
            IncludeLatch = EventOptions.IncludeLatch,
            IncludeMemory = EventOptions.IncludeMemory,
            IncludeCallstack = EventOptions.IncludeCallStack
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
            IncludeLatch = dto.IncludeLatch,
            IncludeMemory = dto.IncludeMemory,
            IncludeCallStack = dto.IncludeCallstack
        };

        ShowLocks = dto.IncludeLock;
        ShowLatches = dto.IncludeLatch;
        ShowWaits = dto.IncludeWait;

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
        PlayheadTimeUs = timeUs;

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

        // Reflect the already-built spans and current playhead position immediately.
        ApplyIndexPageSpans(indexViewModel);
        SyncIndexPage(PlayheadTimeUs);
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

    // Per-index-root spans, built once whenever the event set (re)builds - see RefreshIndexPageSpans -
    // rather than rescanned per playhead tick. Same shape and colouring as the allocation map's own
    // "Events" overlay: each span already carries its own DisplayColour and lifetime (EndUs = the query's
    // end for a read, so it stays once hit; EndUs = the hold end for a latch, so it flashes), so
    // IndexControl just draws whichever span is active - it has no read/latch distinction of its own.
    private readonly Dictionary<PageAddress, IReadOnlyList<PageSpan>> _indexPageSpansByRoot = new();

    // Reads only, kept separately (without colour) purely so SyncIndexPage can binary-search for the
    // active page - a latch doesn't count as "the page being read".
    private readonly Dictionary<PageAddress, IReadOnlyList<PageSpan>> _indexReadSpansByRoot = new();

    /// <summary>
    /// Rebuilds <see cref="_indexPageSpansByRoot"/>/<see cref="_indexReadSpansByRoot"/> from the current
    /// event set and pushes them into every open index tab. Called whenever the event set changes (see
    /// <see cref="RefreshLayers"/>) - not per playhead tick, since none of this depends on the playhead.
    /// </summary>
    private void RefreshIndexPageSpans(List<EngineEvent> engineEvents, EventColourProvider colours)
    {
        _indexPageSpansByRoot.Clear();
        _indexReadSpansByRoot.Clear();

        var queryEndUs = ComputeQueryEndUs(engineEvents);

        var allSpans = new Dictionary<PageAddress, List<PageSpan>>();
        var readSpans = new Dictionary<PageAddress, List<PageSpan>>();

        void Add(PageSpan span, bool isRead)
        {
            if (RootPageOf(span.Address) is not { } root)
            {
                return;
            }

            AddSpan(allSpans, root, span);

            if (isRead)
            {
                AddSpan(readSpans, root, span);
            }
        }

        foreach (var e in engineEvents)
        {
            switch (e)
            {
                case LatchEvent { PageAddress: { } pg } latch:
                    var endUs = latch.TimeUs + Math.Max(latch.DurationUs, MinFlashDurationUs);
                    var latchColour = colours.GetLatchMapColour(e.ObjectName) ?? colours.GetColour(e);
                    Add(new PageSpan(pg, latch.TimeUs, endUs, latchColour), isRead: false);
                    break;

                case IoEvent { IsRead: true, PageAddress: { } pg } io:
                    var readColour = colours.GetObjectColour(e.ObjectName) ?? colours.GetColour(e);
                    Add(new PageSpan(pg, io.TimeUs, queryEndUs, readColour), isRead: true);
                    break;

                case ReadEventGroup group:
                    // A read spans many pages now, so add a read span for each page it touched, timed at the read's END
                    // (the pages hit the buffer on completion, matching the timeline read band's end-bunched rails).
                    var groupColour = colours.GetObjectColour(e.ObjectName) ?? colours.GetColour(e);
                    var groupReadAtUs = e.TimeUs + e.DurationUs;
                    foreach (var page in group.Pages)
                    {
                        Add(new PageSpan(page, groupReadAtUs, queryEndUs, groupColour), isRead: true);
                    }
                    break;
            }
        }

        foreach (var (root, spans) in allSpans)
        {
            _indexPageSpansByRoot[root] = [.. spans.OrderBy(s => s.StartUs)];
        }

        foreach (var (root, spans) in readSpans)
        {
            _indexReadSpansByRoot[root] = [.. spans.OrderBy(s => s.StartUs)];
        }

        foreach (var viewModel in _openIndexes.Values)
        {
            ApplyIndexPageSpans(viewModel);
        }

        SyncIndexPage(PlayheadTimeUs);

        return;

        static void AddSpan(Dictionary<PageAddress, List<PageSpan>> spansByRoot, PageAddress root, PageSpan span)
        {
            if (!spansByRoot.TryGetValue(root, out var list))
            {
                list = [];
                spansByRoot[root] = list;
            }

            list.Add(span);
        }
    }

    private void ApplyIndexPageSpans(IndexTabViewModel viewModel)
    {
        viewModel.PageSpans = _indexPageSpansByRoot.TryGetValue(viewModel.RootPage, out var spans)
            ? spans
            : [];
    }

    private static long ComputeQueryEndUs(List<EngineEvent> engineEvents) =>
        engineEvents.DefaultIfEmpty().Max(e => e?.TimeUs + e?.DurationUs) ?? MinFlashDurationUs;

    /// <summary>
    /// Updates each open index tab's active page (the latest read at or before the playhead) and current
    /// playhead position. The range/flash highlighting itself is computed by IndexControl straight from
    /// the (already time-sorted) spans pushed by RefreshIndexPageSpans, so this only needs to resolve the
    /// single "active" page via binary search - no per-tick event scan.
    /// </summary>
    private void SyncIndexPage(long playheadUs)
    {
        if (_openIndexes.Count == 0)
        {
            return;
        }

        foreach (var viewModel in _openIndexes.Values)
        {
            viewModel.PlayheadTimeUs = playheadUs;

            if (!_indexReadSpansByRoot.TryGetValue(viewModel.RootPage, out var spans) || spans.Count == 0)
            {
                viewModel.SelectedPageAddress = null;
                continue;
            }

            var index = UpperBoundByStartUs(spans, playheadUs) - 1;

            viewModel.SelectedPageAddress = index >= 0 ? spans[index].Address : null;
        }
    }

    /// <summary>The index of the first span whose StartUs is &gt; <paramref name="value"/>.</summary>
    private static int UpperBoundByStartUs(IReadOnlyList<PageSpan> spans, long value)
    {
        var lo = 0;
        var hi = spans.Count;

        while (lo < hi)
        {
            var mid = (lo + hi) / 2;

            if (spans[mid].StartUs <= value)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private static bool NameMatches(string? a, string? b) =>
        string.Equals(a?.Trim('[', ']'), b?.Trim('[', ']'), StringComparison.OrdinalIgnoreCase);

    public Visibility HasEvents
        => Events.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    private List<AllocationLayer> ObjectLayers { get; set; }

    private readonly Dictionary<string, Color> _objectColoursByName;

    public QueryViewModel(ILogger<QueryViewModel> logger,
                          QueryRunner queryRunner,
                          SettingsService settingsService,
                          SettingsViewModel settingsViewModel,
                          IndexTabViewModelFactory indexTabViewModelFactory,
                          DatabaseSource database)
    {
        Logger = logger;
        QueryRunner = queryRunner;
        Database = database;
        _settingsService = settingsService;
        _settingsViewModel = settingsViewModel;
        _indexTabViewModelFactory = indexTabViewModelFactory;
        Message = string.Empty;

        Name = $"{Database.Name}: Query";

        DatabaseFiles = database.Files
                                .Select(f => new DatabaseFile(this) { FileId = f.FileId, Size = f.Size })
                                .ToArray();

        ObjectLayers = AllocationLayerBuilder.GenerateLayers(database, true, 20);

        _objectColoursByName = ObjectLayers
            .Where(l => !string.IsNullOrEmpty(l.Name))
            .GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Colour, StringComparer.OrdinalIgnoreCase);

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

        SqlDocument = DocumentViewModel.Create<SqlDocumentView>("SQL", 
                                                                this, 
                                                                keepAlive: true, 
                                                                key: "Sql");

        AllocationsDocument = DocumentViewModel.Create<AllocationDocumentView>("Allocations", 
                                                                               this, 
                                                                               keepAlive: true, 
                                                                               key: "Allocations");

        PlanDocument = DocumentViewModel.Create<PlanDocumentView>("Execution Plan", 
                                                                  this, 
                                                                  keepAlive: true, 
                                                                  key: "Plan");

        EventsDocument = DocumentViewModel.Create<EventsDocumentView>("Events", 
                                                                      this, 
                                                                      keepAlive: true, 
                                                                      key: "Events");

        CallstackDocument = DocumentViewModel.Create<CallstackDocumentView>("Call Stack", 
                                                                            this, 
                                                                            keepAlive: true, 
                                                                            key: "Callstack");

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

    private readonly SettingsViewModel _settingsViewModel;

    private readonly IndexTabViewModelFactory _indexTabViewModelFactory;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ExecuteQuery(ExecuteSqlPayload payload, CancellationToken cancellationToken)
    {
        ScheduleSaveLayout();

        ClearResults();

        var progress = new Progress<string>(message => Message = string.IsNullOrEmpty(Message)
                                                                 ? message
                                                                 : Message + Environment.NewLine + message);

        // The QueryRunner does the crop now (trimming events and the call stack to the query's window).
        EventOptions.CropToQuery = CropToQuery;

        // Run full trace on background thread
        var (results, layers, colours, startOffset, endOffset) =
            await Task.Run(async () =>
            {
                var queryResult = await QueryRunner.TraceQuery(payload,
                                                               Database,
                                                               EventOptions,
                                                               _settingsViewModel.SymbolsPath,
                                                               progress,
                                                               cancellationToken);

                if (!queryResult.IsSuccess)
                {
                    return (queryResult,
                            new AllocationLayer(),
                            new EventColourProvider([], _objectColoursByName),
                            (long?)null,
                            (long?)null);
                }

                var colourProvider = new EventColourProvider(queryResult.ExecutionPlans, _objectColoursByName);

                // Events are already trimmed to the window; its bounds feed the timeline axis.
                var allocationLayer = GetEventsAllocationLayer(queryResult.EngineEvents,
                                                               colourProvider,
                                                               queryResult.CropStartUs,
                                                               queryResult.CropEndUs);

                return (queryResult, allocationLayer, colourProvider, queryResult.CropStartUs, queryResult.CropEndUs);
            },
            cancellationToken);

        StartOffset = startOffset;
        EndOffset = endOffset;

        if (!results.IsSuccess)
        {
            IsError = true;
            Message = Message + Environment.NewLine + results.Message;

            return;
        }

        IsError = false;
        Message = Message + Environment.NewLine + $"({results.RowCount} rows affected)";

        try
        {
            EventColours = colours;

            Events = results.EngineEvents;

            // The distinct frames now live once in the shared tree rather than per event.
            Callstacks = results.CallStack?.Nodes().Select(n => n.Frame!).ToList() ?? [];

            CallStack = results.CallStack;

            ExecutionPlans = new ObservableCollection<ExecutionPlan>(results.ExecutionPlans);

            ResultSets = results.ResultSets;

            await EventFilter.BuildAsync(Events);

            ShowResultTabsForFirstRun();

            ApplyEventLayers(layers);
            RefreshIndexPageSpans(Events, colours);
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
        PlayheadTimeUs = 0;
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
        ResultSets = [];

        foreach (var indexViewModel in _openIndexes.Values)
        {
            indexViewModel.SelectedPageAddress = null;
            indexViewModel.PageSpans = [];
        }

        _indexPageSpansByRoot.Clear();
        _indexReadSpansByRoot.Clear();

        EventFilter.Clear();

        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);
    }

    private void RefreshLayers(List<EngineEvent> engineEvents)
    {
        ApplyEventLayers(GetEventsAllocationLayer(engineEvents, EventColours, StartOffset, EndOffset));

        RefreshIndexPageSpans(engineEvents, EventColours);
    }

    private void ApplyEventLayers(AllocationLayer layer)
    {
        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers) { layer };
    }

    public void RefreshFilteredEvents()
    {
        FilteredEvents = [.. EventFilter.Apply(Events)];

        RefreshLayers(FilteredEvents);
    }

    private const long MinFlashDurationUs = 200;

    private AllocationLayer GetEventsAllocationLayer(List<EngineEvent> engineEvents,
                                                     EventColourProvider colours,
                                                     long? startOffset,
                                                     long? endOffset)
    {
        var maxFileId = DatabaseFiles.Max(d => d.FileId);

        var overlayLayer = new AllocationLayer { Name = "Events", IsVisible = true };

        var queryEndUs = ComputeQueryEndUs(engineEvents);

        var pageSpans = new List<PageSpan>();

        foreach (var e in engineEvents)
        {
            if (startOffset != null && endOffset != null && (e.TimeUs < startOffset || e.TimeUs > endOffset))
            {
                continue;
            }

            // A read now spans many pages, so flash each page of the group on the allocation map.
            if (e is ReadEventGroup group)
            {
                var readColour = colours.GetObjectColour(e.ObjectName) ?? colours.GetColour(e);

                // The pages hit the buffer when the read COMPLETES, not when it is issued, so light them at the read's
                // end — matching the timeline read band, whose return rails bunch at the end (see EventTimelineControl).
                var readAtUs = e.TimeUs + e.DurationUs;

                foreach (var p in group.Pages)
                {
                    if (p.FileId > 0 && p.FileId <= maxFileId)
                    {
                        pageSpans.Add(new PageSpan(p, readAtUs, queryEndUs, readColour));
                    }
                }

                continue;
            }

            if (e is PageEngineEvent { PageAddress: { FileId: > 0 } page } && page.FileId <= maxFileId)
            {
                if (e is LatchEvent latchEvent)
                {
                    var endUs = latchEvent.TimeUs + Math.Max(latchEvent.DurationUs, MinFlashDurationUs);

                    var displayColour = colours.GetLatchMapColour(e.ObjectName)
                                        ?? colours.GetObjectColour(e.ObjectName)
                                        ?? colours.GetColour(e);

                    var latchSpan = new PageSpan(page, latchEvent.TimeUs, endUs, displayColour);

                    pageSpans.Add(latchSpan);

                    continue;
                }

                var pageSpan = new PageSpan(page,
                                                 e.TimeUs,
                                                 queryEndUs,
                                                 colours.GetObjectColour(e.ObjectName) ?? colours.GetColour(e));

                pageSpans.Add(pageSpan);
            }
        }

        overlayLayer.SetPageSpans([.. pageSpans.OrderBy(s => s.StartUs)]);

        return overlayLayer;
    }

    public PfsChain PfsChain => new();
}
