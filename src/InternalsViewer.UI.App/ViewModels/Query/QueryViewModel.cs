using InternalsViewer.UI.App.Services.Query.Trace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Query;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Controls.SqlEditor;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Schema;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.Services.XEvents;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Query.Trace;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.ViewModels.Query.Events;
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
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.TransactionLog.LogRecords;
using InternalsViewer.UI.App.ViewModels.Page;
using InternalsViewer.UI.App.ViewModels.Query.Settings;
using InternalsViewer.UI.App.Views.Query.Tabs.Trace;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Views.Query.Tabs.Index;
using DatabaseFile = InternalsViewer.UI.App.Models.DatabaseFile;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed class QueryViewModelFactory(ILogger<QueryViewModel> logger,
                                          QueryRunner queryRunner,
                                          SettingsService settingsService,
                                          SettingsViewModel settingsViewModel,
                                          IndexTabViewModelFactory indexTabViewModelFactory,
                                          PageTabViewModelFactory pageTabViewModelFactory,
                                          TraceDirectoryService traceDirectoryService,
                                          IBufferPoolInfoProvider bufferPoolInfoProvider,
                                          TraceTabViewModelFactory traceTabViewModelFactory)
{
    public QueryViewModel Create(DatabaseSource database) => new(logger,
                                                                 queryRunner,
                                                                 settingsService,
                                                                 settingsViewModel,
                                                                 indexTabViewModelFactory,
                                                                 pageTabViewModelFactory,
                                                                 traceDirectoryService,
                                                                 bufferPoolInfoProvider,
                                                                 traceTabViewModelFactory,
                                                                 database);
}

public sealed partial class QueryViewModel : TabViewModel, IAllocationViewModel
{
    private ILogger<QueryViewModel> Logger { get; }

    private QueryRunner QueryRunner { get; }

    private IBufferPoolInfoProvider BufferPoolInfoProvider { get; }

    public DatabaseSource Database { get; }

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
    private IReadOnlyList<AllocationBorder> _allocationBorders = [];

    [ObservableProperty]
    private bool _isTooltipEnabled = true;

    [ObservableProperty]
    private PfsChain _pfsChain = new();

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
    private bool _showBufferPool;

    /// <summary>
    /// The query capture and display options (crop, system objects, lock categories, waits/latches/memory/call stack)
    /// </summary>
    public QueryOptionsViewModel QueryOptions { get; } = new();

    /// <summary>
    /// The dock layout — tab documents, their menu-driven visibility, and the timeline/details rows
    /// </summary>
    public QueryLayoutViewModel Layout { get; }

    [ObservableProperty]
    private int _extentCount;

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

    [ObservableProperty]
    private DatabaseSchema? _schema;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveResultSet))]
    private List<QueryResultSet> _resultSets = [];

    public QueryResultSet? ActiveResultSet => ResultSets.Count > 0 ? ResultSets[0] : null;

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

    public event Action<EngineEvent>? EventNavigationRequested;

    public void NavigateToEvent(EngineEvent engineEvent)
    {
        SelectedEvent = engineEvent;

        Layout.IsEventsVisible = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            EventNavigationRequested?.Invoke(engineEvent);
        });
    }

    private const string LayoutSettingKey = "QueryDockLayout";

    private bool _isRestoringLayout;
    private bool _layoutRestored;
    private bool _layoutTouched;
    private bool _saveScheduled;

    private void OnLayoutChanged()
    {
        if (!_isRestoringLayout)
        {
            _layoutTouched = true;
        }

        PruneClosedIndexes();

        PruneClosedPages();

        PruneClosedTraces();

        ScheduleSaveLayout();
    }

    private void ScheduleSaveLayout()
    {
        if (_isRestoringLayout || _saveScheduled)
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
            Root = Layout.SerializeRoot(),
            TimelineVisible = Layout.IsTimelineVisible,
            CropToQuery = QueryOptions.CropToQuery,
            IncludeSystemObjects = QueryOptions.IncludeSystemObjects,
            IncludeLock = QueryOptions.Options.IncludeLock,
            IncludeWait = QueryOptions.Options.IncludeWait,
            IncludeLatch = QueryOptions.Options.IncludeLatch,
            IncludeMemory = QueryOptions.Options.IncludeMemory,
            IncludeCallstack = QueryOptions.Options.IncludeCallStack,
            LockModeCategories = [.. QueryOptions.Options.IncludeLockModeCategories]
        };

        await _settingsService.SaveSettingAsync(LayoutSettingKey, dto);
    }

    private async Task RestoreLayoutAsync()
    {
        var dto = await _settingsService.ReadSettingAsync<QueryLayoutState>(LayoutSettingKey);

        if (dto is null || _layoutTouched)
        {
            return;
        }

        _isRestoringLayout = true;

        try
        {
            if (!Layout.RestoreRoot(dto.Root))
            {
                return;
            }

            Layout.IsTimelineVisible = dto.TimelineVisible;

            var lockCategories = dto.LockModeCategories is { } categories
                                 ? [.. categories.Where(c => c != LockModeCategory.None)]
                                 : dto.IncludeLock ? EventOptions.DefaultLockModeCategories() : [];

            QueryOptions.Restore(dto.CropToQuery,
                                 dto.IncludeSystemObjects,
                                 dto.IncludeWait,
                                 dto.IncludeLatch,
                                 dto.IncludeMemory,
                                 dto.IncludeCallstack,
                                 lockCategories);

            _layoutRestored = true;
        }
        finally
        {
            _isRestoringLayout = false;
        }
    }

    [RelayCommand]
    private void ResetLayout()
    {
        _layoutRestored = false;
        _resultTabsOpened = false;

        Layout.Reset();
    }

    [ObservableProperty]
    private ObservableCollection<ExecutionPlan> _executionPlans = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPlanNodeEventStatistics))]
    [NotifyPropertyChangedFor(nameof(SelectedPlanExpressions))]
    [NotifyPropertyChangedFor(nameof(SelectedPlanNodeScanMode))]
    private PlanNode? _selectedPlanNode;

    public ScanModeResult? SelectedPlanNodeScanMode
    {
        get
        {
            if (SelectedPlanNode is not { } node || node.ScanInfo is null || string.IsNullOrEmpty(node.Table))
            {
                return null;
            }

            return ScanModeDetector.Detect(node, FindAllocationUnit(node), Events);
        }
    }

    private readonly TraceTabViewModelFactory _traceTabViewModelFactory;

    private const string TraceDocumentKey = "Trace";

    private readonly Dictionary<string, TraceTabViewModel> _openTraces = new();

    private int? _traceTargetNodeId;

    private Internals.Engine.Database.AllocationUnit? FindAllocationUnit(PlanNode node)
    {
        if (string.IsNullOrEmpty(node.Table))
        {
            return null;
        }

        return Database.AllocationUnits
                       .Values
                       .FirstOrDefault(a => NameMatches(a.IndexName, node.Index ?? string.Empty)
                                            && NameMatches(a.TableName, node.Table)
                                            && (string.IsNullOrEmpty(node.Schema) || NameMatches(a.SchemaName, node.Schema))
                                            && a.AllocationUnitType == AllocationUnitType.InRowData);
    }

    public void OpenTrace(PlanNodeIdentifier identifier)
    {
        if (ResolvePlanNode(identifier) is { } node)
        {
            OpenTrace(node);
        }
    }

    /// <summary>
    /// Opens a trace of an operator and everything below it
    /// </summary>
    /// <remarks>
    /// The plan node is the whole identity of a trace, so one document is kept per operator and the kind of operator no longer decides
    /// anything here. An operator with something below it that cannot be simulated builds no definition and opens nothing, which is the
    /// same test the context menu uses to decide whether to offer the command.
    /// </remarks>
    public bool OpenTrace(PlanNode node)
    {
        if (node.IsStatement)
        {
            return node.Children.FirstOrDefault() is { } child && OpenTraceCore(child, wrapInSelect: true);
        }

        return OpenTraceCore(node, wrapInSelect: false);
    }

    [RelayCommand]
    public void OpenTrace()
    {
        if (SelectedPlanNode is { } node && CanTrace(node))
        {
            OpenTrace(node);

            return;
        }

        var root = ExecutionPlans.FirstOrDefault(p => !p.IsInternalPlan)?.Root.FirstOrDefault();

        if (root is not null)
        {
            OpenTrace(root);
        }
    }

    private bool OpenTraceCore(PlanNode node, bool wrapInSelect)
    {
        var targetNodeId = wrapInSelect ? -1 : node.NodeId;

        if (_traceTargetNodeId == targetNodeId && Layout.TryGetDocument(TraceDocumentKey, out var existing))
        {
            Layout.Show(existing);

            return true;
        }

        var scanMode = FindAllocationUnit(node) is { } unit ? ScanModeDetector.Detect(node, unit, Events) : null;

        var traceViewModel = _traceTabViewModelFactory.Create(Database,
                                                              node,
                                                              FindAllocationUnit,
                                                              Events.FirstOrDefault()?.Timestamp,
                                                              scanMode,
                                                              wrapInSelect);

        if (traceViewModel is null)
        {
            Logger.LogWarning("Operator {NodeId} ({Operator}) cannot be traced", node.NodeId, node.PhysicalOperator);

            return false;
        }

        CloseTraceDocument();

        traceViewModel.IsNestedLayout = _settingsViewModel.TraceNestedLayout;

        traceViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TraceTabViewModel.IsNestedLayout))
            {
                _settingsViewModel.TraceNestedLayout = traceViewModel.IsNestedLayout;
            }
        };

        traceViewModel.PageNavigated += OnIndexPageNavigated;

        traceViewModel.PageOpenRequested += OnTracePageOpenRequested;

        var title = wrapInSelect ? "Trace: SELECT" : $"Trace: {TraceTitle(node, traceViewModel)}";

        var document = new DocumentViewModel(title: title,
                                             content: traceViewModel,
                                             viewFactory: static () => new TraceTabView(),
                                             canClose: true,
                                             keepAlive: true,
                                             key: TraceDocumentKey,
                                             persist: false,
                                             commandsFactory: static () => new TraceTabCommands());

        Layout.RegisterDocument(TraceDocumentKey, document);

        _openTraces[TraceDocumentKey] = traceViewModel;

        _traceTargetNodeId = targetNodeId;

        Layout.Show(document);

        OnPropertyChanged(nameof(IsTraceVisible));

        return true;
    }

    /// <summary>
    /// Closes the trace, which is one document per query rather than one per operator traced
    /// </summary>
    /// <remarks>
    /// Dropping the document from tracking leaves it in the dock, so the tab has to be closed as well. Tracking is cleared first, which
    /// is what stops the layout change that closing raises from pruning a trace that is about to be replaced.
    /// </remarks>
    private void CloseTraceDocument()
    {
        if (_openTraces.Remove(TraceDocumentKey, out var viewModel))
        {
            viewModel.PageNavigated -= OnIndexPageNavigated;
            viewModel.PageOpenRequested -= OnTracePageOpenRequested;
        }

        _traceTargetNodeId = null;

        if (Layout.RemoveDocument(TraceDocumentKey, out var document))
        {
            Layout.Close(document);

            document.DisposeView();
        }

        OnPropertyChanged(nameof(IsTraceVisible));
    }

    /// <summary>
    /// Whether the trace is open, which the View menu shows and toggles
    /// </summary>
    public bool IsTraceVisible
    {
        get => Layout.IsShown(TraceDocumentKey);
        set
        {
            if (value)
            {
                OpenTrace();
            }
            else
            {
                CloseTraceDocument();
            }

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Whether a trace can be offered for an operator, which needs every operator below it to be one we simulate
    /// </summary>
    public bool CanTrace(PlanNode node)
        => node.IsStatement
            ? node.Children.FirstOrDefault() is { } child && new TraceDefinitionBuilder(FindAllocationUnit).CanBuild(child)
            : new TraceDefinitionBuilder(FindAllocationUnit).CanBuild(node);

    private static string TraceTitle(PlanNode node, TraceTabViewModel viewModel)
    {
        var objects = viewModel.Visuals.Select(v => v.AllocationUnit.TableName).Distinct().ToList();

        return objects.Count == 1 ? objects[0] ?? node.PhysicalOperator : $"{node.PhysicalOperator} {string.Join("/", objects)}";
    }

    public ExpressionCatalog? SelectedPlanExpressions
    {
        get
        {
            if (SelectedPlanNode is not { } node)
            {
                return null;
            }

            var plan = ExecutionPlans.FirstOrDefault(p => p.NodesById.TryGetValue(node.NodeId, out var candidate)
                                                          && ReferenceEquals(candidate, node))
                       ?? ExecutionPlans.FirstOrDefault();

            return plan?.Expressions;
        }
    }

    /// <summary>
    /// Read activity captured for the selected operator, aggregated from its linked read events
    /// </summary>
    public EventIoStatistics? SelectedPlanNodeEventStatistics
    {
        get
        {
            if (SelectedPlanNode is not { } node)
            {
                return null;
            }

            var reads = Events.OfType<ReadEventGroup>()
                              .Where(e => e.PlanNodeIdentifier?.NodeId == node.NodeId)
                              .ToList();

            if (reads.Count == 0)
            {
                return null;
            }

            var physicalReads = reads.Count(r => r.ReadType == ReadType.NonCached);

            var readAheads = reads.Where(r => r.ReadType == ReadType.NonCached && r.Pages.Count > 1)
                                  .Sum(r => (long)r.Pages.Count);

            return new EventIoStatistics(reads.Count, physicalReads, readAheads);
        }
    }

    [ObservableProperty]
    private IReadOnlyList<PlanNode> _activePlanNodes = [];

    [ObservableProperty]
    private IReadOnlyList<PlanNode> _emittingPlanNodes = [];

    public void SetScope(long fromUs, long toUs)
    {
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

            if (e.SequenceId < from)
            {
                from = e.SequenceId;
            }

            if (e.SequenceId > to)
            {
                to = e.SequenceId;
            }
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

    private void UpdateActiveOperators(long timeUs)
    {
        var source = FilteredEvents.Count > 0 ? FilteredEvents : Events;

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

        if (Events.FirstOrDefault(e => e is ExecutionOperatorEvent && e.PlanNodeIdentifier == identifier) is { } op)
        {
            SelectedEvent = op;
        }
    }

    [ObservableProperty]
    private bool _isPlanPropertiesVisible;

    private bool _isFlameGraphVisible = true;

    public bool IsFlameGraphVisible
    {
        get => _isFlameGraphVisible;
        set => SetProperty(ref _isFlameGraphVisible, value);
    }

    private bool _isSqlResultsVisible;

    public bool IsSqlResultsVisible
    {
        get => _isSqlResultsVisible;
        set => SetProperty(ref _isSqlResultsVisible, value);
    }

    private bool _isSqlMessagesVisible;

    public bool IsSqlMessagesVisible
    {
        get => _isSqlMessagesVisible;
        set => SetProperty(ref _isSqlMessagesVisible, value);
    }

    public void OpenExecutionPlan(PlanNodeIdentifier identifier)
    {
        Layout.ShowExecutionPlan();

        SelectPlanNode(identifier);

        IsPlanPropertiesVisible = true;
    }

    private readonly Dictionary<string, IndexTabViewModel> _openIndexes = new();


    [RelayCommand]
    public void OpenIndexes()
    {
        foreach (var op in ExecutionPlans.SelectMany(e => e.NodesById.Values)
                                         .Where(v => !string.IsNullOrEmpty(v.Index) && 
                                                     v.PhysicalOperator is "Index Seek" 
                                                         or "Index Scan"
                                                         or "Clustered Index Seek" 
                                                         or "Clustered Index Scan"
                                                         or "Key Lookup (Clustered)" 
                                                         or "RID Lookup"))
        {
            OpenIndex(op);
        }
    }

    public void OpenIndex(ExecutionOperatorEvent op)
    {
        if (op.PlanNodeIdentifier is not null)
        {
            var planNode = ResolvePlanNode(op.PlanNodeIdentifier);

            if (planNode is not null)
            {
                OpenIndex(planNode);

                return;
            }
        }

        OpenIndex(op.SchemaName, op.TableName, op.IndexName);
    }

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

        if (Layout.TryGetDocument(key, out var existing))
        {
            Layout.Show(existing);

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
        indexViewModel.AllocationUnit = allocationUnit;

        var document = new DocumentViewModel(title: $"Index: {index}",
                                             content: indexViewModel,
                                             viewFactory: static () => new QueryIndexTabView(),
                                             canClose: true,
                                             keepAlive: true,
                                             key: key,
                                             persist: false,
                                             commandsFactory: static () => new QueryIndexTabCommands());

        Layout.RegisterDocument(key, document);

        _openIndexes[key] = indexViewModel;


        Layout.Show(document);

        // Reflect the already-built spans and current playhead position immediately.
        ApplyIndexPageSpans(indexViewModel);

        SyncIndexPage(PlayheadTimeUs);
    }

    private readonly Dictionary<string, PageTabViewModel> _openPages = new();

    /// <summary>
    /// The captured log records for a page, matched from the query's transaction log events
    /// </summary>
    public List<PageLogRecord> GetPageLogRecords(PageAddress pageAddress)
    {
        return
        [
            .. Events.OfType<TransactionLogEvent>()
                .Where(logEvent => logEvent.PageAddress == pageAddress)
                .Select(logEvent => logEvent.LogRecord)
                .OfType<PageLogRecord>()
        ];
    }

    /// <summary>
    /// Opens a page as a document tab inside the query view's dock layout, with any captured log records for it
    /// </summary>
    /// <remarks>
    /// Page documents are transient like index tabs - keyed by page address so a repeat open focuses and reloads
    /// the existing tab, and pruned when the user closes them
    /// </remarks>
    /// <summary>
    /// Opens the page for an event, when the event is tied to a page
    /// </summary>
    public void OpenEventPage(EngineEvent engineEvent)
    {
        if (engineEvent is PageEngineEvent { PageAddress: { } pageAddress })
        {
            OpenPage(pageAddress);
        }
    }

    public void OpenPage(PageAddress pageAddress)
    {
        var logRecords = GetPageLogRecords(pageAddress);

        var key = $"Page:{pageAddress}";

        if (Layout.TryGetDocument(key, out var existing))
        {
            Layout.Show(existing);

            if (_openPages.TryGetValue(key, out var openViewModel))
            {
                _ = LoadPageDocument(openViewModel, pageAddress, logRecords);
            }

            return;
        }

        var pageViewModel = _pageTabViewModelFactory.Create(Database);

        var document = new DocumentViewModel(title: $"Page {pageAddress}",
                                             content: pageViewModel,
                                             viewFactory: static () => new QueryPageTabView(),
                                             canClose: true,
                                             keepAlive: true,
                                             key: key,
                                             persist: false);

        Layout.RegisterDocument(key, document);

        _openPages[key] = pageViewModel;

        Layout.Show(document);

        _ = LoadPageDocument(pageViewModel, pageAddress, logRecords);
    }

    private async Task LoadPageDocument(PageTabViewModel pageViewModel,
                                        PageAddress pageAddress,
                                        IReadOnlyList<PageLogRecord> logRecords)
    {
        try
        {
            await pageViewModel.LoadPage(pageAddress, null);

            pageViewModel.LogRecords = new ObservableCollection<LogRecordItem>(
                logRecords.Select(r => new LogRecordItem { Record = r }));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error opening page {PageAddress}", pageAddress);
        }
    }

    /// <summary>
    /// Drops page tabs the user has closed (they are transient and recreated on demand)
    /// </summary>
    private void PruneClosedPages()
    {
        if (_openPages.Count == 0)
        {
            return;
        }

        var closed = _openPages.Keys
            .Where(k => !Layout.IsShown(k))
            .ToList();

        foreach (var key in closed)
        {
            _openPages.Remove(key);

            if (Layout.RemoveDocument(key, out var document))
            {
                document.DisposeView();
            }
        }
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
            .Where(k => !Layout.IsShown(k))
            .ToList();

        foreach (var key in closed)
        {

            _openIndexes.Remove(key);

            if (Layout.RemoveDocument(key, out var document))
            {
                document.DisposeView();
            }
        }
    }

    private void PruneClosedTraces()
    {
        if (_openTraces.Count == 0)
        {
            return;
        }

        var closed = _openTraces.Keys
            .Where(k => !Layout.IsShown(k))
            .ToList();

        foreach (var key in closed)
        {
            _openTraces[key].PageNavigated -= OnIndexPageNavigated;
            _openTraces[key].PageOpenRequested -= OnTracePageOpenRequested;

            _openTraces.Remove(key);

            _traceTargetNodeId = null;

            if (Layout.RemoveDocument(key, out var document))
            {
                document.DisposeView();
            }

            OnPropertyChanged(nameof(IsTraceVisible));
        }
    }

    /// <summary>
    /// Points the open trace at the plan the query just produced, so one trace follows the query rather than outliving it
    /// </summary>
    /// <remarks>
    /// A rerun of the same statement still has the operator that was being traced, so the trace is rebuilt on it. A different query has
    /// its own plan and the node traced may be gone from it, which falls back to the root rather than leaving the old plan's trace open.
    /// </remarks>
    private void RefreshTraceDocuments()
    {
        if (_openTraces.Count == 0 || _traceTargetNodeId is not { } target)
        {
            return;
        }

        var root = ExecutionPlans.FirstOrDefault(p => !p.IsInternalPlan)?.Root.FirstOrDefault();

        var node = target < 0
            ? root
            : ExecutionPlans.Where(p => !p.IsInternalPlan)
                            .Select(p => p.NodesById.GetValueOrDefault(target))
                            .FirstOrDefault(n => n is not null) ?? root;

        if (node is null || !CanTrace(node))
        {
            CloseTraceDocument();

            return;
        }

        _traceTargetNodeId = null;

        if (!OpenTrace(node))
        {
            CloseTraceDocument();
        }
    }

    public event Action<long>? PlayheadMoveRequested;

    private void OnTracePageOpenRequested(object? sender, PageAddress pageAddress) => OpenPage(pageAddress);

    private void OnIndexPageNavigated(object? sender, PageNavigatedEventArgs e)
    {
        var readEndUs = GetReadEndUs(e.PageAddress, e.IsReset ? null : PlayheadTimeUs, e.Selection);

        if (readEndUs is not null)
        {
            SetPlayheadTime(readEndUs.Value);

            PlayheadMoveRequested?.Invoke(readEndUs.Value);
        }
    }

    private long? GetReadEndUs(PageAddress pageAddress, long? afterUs, PageReadSelection selection = PageReadSelection.Next)
    {
        long? first = null;
        long? last = null;
        long? next = null;

        void Consider(long endUs)
        {
            if (first is null || endUs < first)
            {
                first = endUs;
            }

            if (last is null || endUs > last)
            {
                last = endUs;
            }

            if (afterUs is { } after && endUs > after && (next is null || endUs < next))
            {
                next = endUs;
            }
        }

        foreach (var engineEvent in Events)
        {
            if (engineEvent is not ReadEventGroup group)
            {
                continue;
            }

            if (group.ReadType == ReadType.Cached)
            {
                foreach (var latch in group.Events.OfType<LatchEvent>())
                {
                    if (latch.PageAddress == pageAddress)
                    {
                        Consider(latch.TimeUs + latch.DurationUs);
                    }
                }
            }
            else if (group.Pages.Contains(pageAddress))
            {
                Consider(group.TimeUs + group.DurationUs);
            }
        }

        return selection switch
        {
            PageReadSelection.First => first,
            PageReadSelection.Last => last,
            _ => next ?? first
        };
    }

    private List<PageSpan> _pageSpans = [];

    /// <summary>
    /// Rebuilds <see cref="_pageSpans"/> from the current event set and pushes the spans relevant to
    /// each open index tab. Called whenever the event set changes (see <see cref="RefreshLayers"/>) -
    /// not per playhead tick, since none of this depends on the playhead.
    /// </summary>
    private void RefreshIndexPageSpans(List<EngineEvent> engineEvents, EventColourProvider colours)
    {
        _pageSpans = PageSpanBuilder.GetEventsPageSpans(engineEvents, colours, StartOffset, EndOffset, Database);

        foreach (var viewModel in _openIndexes.Values)
        {
            ApplyIndexPageSpans(viewModel);
        }

        SyncIndexPage(PlayheadTimeUs);
    }

    private void ApplyIndexPageSpans(IndexTabViewModel viewModel)
    {
        viewModel.PageSpans = [.. _pageSpans.Where(s => IsInIndex(s.Address, viewModel.RootPage))];
    }

    private bool IsInIndex(PageAddress page, PageAddress rootPage) =>
        Database.FindPageAllocationUnit(page)?.RootPage == rootPage;

    private void SyncIndexPage(long playheadUs)
    {
        if (_openIndexes.Count == 0)
        {
            return;
        }

        foreach (var viewModel in _openIndexes.Values)
        {
            viewModel.PlayheadTimeUs = playheadUs;

            var spans = viewModel.PageSpans;

            if (spans.Count == 0)
            {
                viewModel.SelectedPageAddress = null;
                continue;
            }

            var index = UpperBoundByStartUs(spans, playheadUs) - 1;

            viewModel.SelectedPageAddress = index >= 0 ? spans[index].Address : null;
        }
    }

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

    // Generating the object layers walks the whole allocation map, so build them off the UI thread and
    // apply the results back on it.
    private async Task LoadObjectLayersAsync(DatabaseSource database)
    {
        try
        {
            var layers = await Task.Run(() => AllocationLayerBuilder.GenerateLayers(database, true, 20));

            ObjectLayers = layers;

            foreach (var group in layers.Where(l => !string.IsNullOrEmpty(l.Name))
                                        .GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase))
            {
                _objectColoursByName[group.Key] = group.First().Colour;
            }

            AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate object layers for database: {Name}", database.Name);
        }
    }

    public QueryViewModel(ILogger<QueryViewModel> logger,
                          QueryRunner queryRunner,
                          SettingsService settingsService,
                          SettingsViewModel settingsViewModel,
                          IndexTabViewModelFactory indexTabViewModelFactory,
                          PageTabViewModelFactory pageTabViewModelFactory,
                          TraceDirectoryService traceDirectoryService,
                          IBufferPoolInfoProvider bufferPoolInfoProvider,
                          TraceTabViewModelFactory traceTabViewModelFactory,
                          DatabaseSource database)
    {
        _traceTabViewModelFactory = traceTabViewModelFactory;
        Logger = logger;
        QueryRunner = queryRunner;
        BufferPoolInfoProvider = bufferPoolInfoProvider;
        Database = database;
        _settingsService = settingsService;
        _settingsViewModel = settingsViewModel;
        _indexTabViewModelFactory = indexTabViewModelFactory;
        _pageTabViewModelFactory = pageTabViewModelFactory;
        _traceDirectoryService = traceDirectoryService;
        Message = string.Empty;

        Name = $"{Database.Name}: Query";

        DatabaseFiles =
        [
            .. database.Files
                .Select(f => new DatabaseFile(this) { FileId = f.FileId, Size = f.Size })
        ];

        ObjectLayers = [];

        _objectColoursByName = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

        ExtentCount = database.GetFilePageCount(1) / 8;

        AllocationLayers = [];

        PfsChain = Database.Pfs[1];

        _ = LoadObjectLayersAsync(database);

        _systemObjectIds =
        [
            .. database.AllocationUnits
                       .Values
                       .Where(u => u.IsSystem)
                       .Select(u => u.ObjectId)
        ];

        QueryOptions.FilterChanged += RefreshFilteredEvents;

        QueryOptions.Changed += ScheduleSaveLayout;

        Schema = SchemaHelper.ToSqlSchema(database);

        Layout = new QueryLayoutViewModel(this);

        Layout.Changed += OnLayoutChanged;
        Layout.SelectionChanged += ScheduleSaveLayout;

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

    private readonly PageTabViewModelFactory _pageTabViewModelFactory;

    private readonly TraceDirectoryService _traceDirectoryService;

    [RelayCommand]
    private async Task ToggleBufferPool(bool isSelected)
    {
        ShowBufferPool = isSelected;

        var layer = AllocationLayers.FirstOrDefault(l => l.LayerName == "Buffer Pool");

        if (layer == null)
        {
            return;
        }

        if (isSelected)
        {
            await RefreshBufferPool();
        }
        else
        {
            layer.Opacity = 0;
            AllocationLayers = [.. AllocationLayers];
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ExecuteQuery(ExecuteSqlPayload payload, CancellationToken cancellationToken)
    {
        ScheduleSaveLayout();

        ClearResults();

        var progress = new Progress<string>(message => Message = string.IsNullOrEmpty(Message)
                                                                 ? message
                                                                 : Message + Environment.NewLine + message);

        var eventOptions = QueryOptions.Options;

        var traceDirectory = _settingsViewModel.ActiveTraceDirectory;

        eventOptions.TraceDirectory = traceDirectory;

        eventOptions.MaxTraceSizeMb = (int)_settingsViewModel.MaxTraceSizeMb;

        eventOptions.AutoDeleteTrace = _settingsViewModel.AutoDeleteTrace;

        // Run full trace on background thread
        var (results, colours, startOffset, endOffset) =
            await Task.Run(async () =>
            {
                if (traceDirectory is not null)
                {
                    _traceDirectoryService.GrantPermissions(traceDirectory);
                }

                var queryResult = await QueryRunner.TraceQuery(payload,
                                                               Database,
                                                               eventOptions,
                                                               _settingsViewModel.SymbolsPath,
                                                               progress,
                                                               cancellationToken);

                if (!queryResult.IsSuccess)
                {
                    return (queryResult,
                            new EventColourProvider([], _objectColoursByName),
                            (long?)null,
                            (long?)null);
                }

                var colourProvider = new EventColourProvider(queryResult.ExecutionPlans, _objectColoursByName);

                return (queryResult, colourProvider, queryResult.CropStartUs, queryResult.CropEndUs);
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

            CallStack = results.CallStackTree;

            ExecutionPlans = new ObservableCollection<ExecutionPlan>(results.ExecutionPlans);

            RefreshTraceDocuments();

            ResultSets = results.ResultSets;

            ShowResultTabsForFirstRun();

            RefreshFilteredEvents();

            if (ShowBufferPool)
            {
                await Task.Run(async () => { await RefreshBufferPool(); }, cancellationToken);
            }
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

        Layout.IsAllocationsVisible = true;
        Layout.IsExecutionPlanVisible = true;
        Layout.IsEventsVisible = true;
    }

    private void ClearResults()
    {
        IsError = false;
        Message = string.Empty;

        SequenceFrom = 0;
        SequenceTo = 0;
        PlayheadTimeUs = 0;
        IsTimelinePlaying = false;
        SelectedPlanNode = null;
        ActivePlanNodes = [];
        EmittingPlanNodes = [];

        Events = [];
        FilteredEvents = [];
        CallStack = null;
        SelectedEvent = null;
        ExecutionPlans = [];
        ResultSets = [];

        foreach (var indexViewModel in _openIndexes.Values)
        {
            indexViewModel.SelectedPageAddress = null;
            indexViewModel.PageSpans = [];
        }

        _pageSpans = [];

        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers);
        AllocationBorders = [];
    }

    private void RefreshLayers(List<EngineEvent> engineEvents)
    {
        ApplyEventLayers(GetEventsAllocationLayer(engineEvents, EventColours, StartOffset, EndOffset));

        RefreshIndexPageSpans(engineEvents, EventColours);

        RefreshLockBorders();
    }

    private void ApplyEventLayers(AllocationLayer layer)
    {
        AllocationLayers = new ObservableCollection<AllocationLayer>(ObjectLayers) { layer };
    }

    public void RefreshFilteredEvents()
    {
        FilteredEvents = [.. Events.Where(IsEventVisible)];

        RefreshLayers(FilteredEvents);
    }

    private bool IsEventVisible(EngineEvent engineEvent)
    {
        if (!QueryOptions.IncludeSystemObjects && !IsUserObject(engineEvent))
        {
            return false;
        }

        return engineEvent switch
        {
            LockEvent l => QueryOptions.Includes(LockModeClassifier.Categorise(l.LockMode)),

            LockGroup g => g.Events.OfType<LockEvent>()
                            .Any(l => QueryOptions.Includes(LockModeClassifier.Categorise(l.LockMode))),

            LockEscalationEvent esc => QueryOptions.Includes(LockModeClassifier.Categorise(esc.LockMode)),

            _ => true,
        };
    }

    private bool IsUserObject(EngineEvent engineEvent)
    {
        if (engineEvent is LockEvent { Resource.ResourceType: LockResourceType.Metadata or LockResourceType.Database })
        {
            return false;
        }

        return engineEvent.ObjectId == 0 || !SystemObjectIds.Contains(engineEvent.ObjectId);
    }

    private AllocationLayer GetEventsAllocationLayer(List<EngineEvent> engineEvents,
                                                     EventColourProvider colours,
                                                     long? startOffset,
                                                     long? endOffset)
    {
        var overlayLayer = new AllocationLayer { Name = "Events", IsVisible = true };

        var pageSpans = PageSpanBuilder.GetEventsPageSpans(engineEvents,
                                                           colours,
                                                           startOffset,
                                                           endOffset,
                                                           Database);

        overlayLayer.SetPageSpans([.. pageSpans.OrderBy(s => s.StartUs)]);

        return overlayLayer;
    }

    private async Task RefreshBufferPool()
    {
        var layer = AllocationLayers.FirstOrDefault(l => l.LayerName == "Buffer Pool");

        if (!ShowBufferPool)
        {
            return;
        }

        try
        {
            var (clean, dirty) = await BufferPoolInfoProvider.GetBufferPoolEntries(Database);

            DispatcherQueue.TryEnqueue(() =>
            {
                if (layer != null)
                {
                    layer.Opacity = 80;
                    layer.SinglePages = [.. dirty, .. clean];

                    AllocationLayers = [.. AllocationLayers];
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh buffer pool overlay for database: {Name}", Database.Name);
        }
    }

    private void RefreshLockBorders()
    {
        AllocationBorders = QueryOptions.ShowLocks && FilteredEvents is { Count: > 0 }
            ? EventBorderBuilder.GetLockBorders(FilteredEvents.SelectMany(FlattenLocks).ToList(), Database)
            : [];
    }

    private static IEnumerable<LockEvent> FlattenLocks(EngineEvent e) => e switch
    {
        LockEvent l => [l],
        LockGroup g => g.Events.OfType<LockEvent>(),
        _ => [],
    };

    /// <summary>
    /// Releases the query event data and disposes the dock layout's views when the tab closes
    /// </summary>
    public override void Dispose()
    {
        QueryOptions.FilterChanged -= RefreshFilteredEvents;
        QueryOptions.Changed -= ScheduleSaveLayout;
        Layout.Changed -= OnLayoutChanged;
        Layout.SelectionChanged -= ScheduleSaveLayout;

        Events = [];
        FilteredEvents = [];
        CallStack = null;
        SelectedEvent = null;

        _openIndexes.Clear();

        _pageSpans = [];

        Layout.Dispose();

        base.Dispose();
    }
}
