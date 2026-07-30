using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Allocations;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Parsing.Plans;
using System.Drawing;
using InternalsViewer.UI.App.Models.Index;
using AllocationBorder = InternalsViewer.UI.App.Models.AllocationBorder;
using AllocationBorderScope = InternalsViewer.UI.App.Models.AllocationBorderScope;
using AllocationLayer = InternalsViewer.UI.App.Models.AllocationLayer;
using TimedRange = InternalsViewer.UI.App.Models.TimedRange;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Views.Query.Tabs.Trace;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace InternalsViewer.UI.App.ViewModels.Query;

public enum TraceKind
{
    Index,
    Allocation
}

public sealed class TraceTabViewModelFactory(IndexStepService indexStepService,
                                             AllocationStepService allocationStepService,
                                             IndexService indexService)
{
    public TraceTabViewModel Create(TraceKind kind,
                                    DatabaseSource database,
                                    AllocationUnit allocationUnit,
                                    PlanNode? planNode,
                                    DateTime? queryTime,
                                    ScanModeResult? scanMode)
    {
        IStepService service = kind == TraceKind.Index ? indexStepService : allocationStepService;

        return new TraceTabViewModel(kind, service, indexService, database, allocationUnit, planNode, queryTime, scanMode);
    }
}

public sealed partial class TraceTabViewModel : ObservableObject
{
    private const int RunStepDelayMs = 150;

    private const int HistoryLimit = 1000;

    public TraceTabViewModel(TraceKind kind,
                             IStepService stepService,
                             IndexService indexService,
                             DatabaseSource database,
                             AllocationUnit allocationUnit,
                             PlanNode? planNode,
                             DateTime? queryTime,
                             ScanModeResult? scanMode)
    {
        Kind = kind;
        StepService = stepService;
        IndexService = indexService;
        Database = database;
        AllocationUnit = allocationUnit;
        PlanNode = planNode;
        QueryTime = queryTime;
        ScanMode = scanMode;

        Dock = BuildDock();

        Dock.LayoutChanged += (_, _) => OnDockLayoutChanged();

        Strategy = SeekDescription;
    }

    public SvgImageSource? IconSource => PlanNode is null ? null : new SvgImageSource(PlanIconResolver.Resolve(PlanNode));

    public DockLayoutViewModel Dock { get; }

    private DockLayoutViewModel BuildDock()
    {
        var visualTitle = Kind == TraceKind.Index ? "Index" : "Allocations";

        var visual = DocumentViewModel.Create<TraceVisualPanelView>(visualTitle, this, canClose: false, keepAlive: true, key: "Visual");
        var steps = DocumentViewModel.Create<TraceStepsPanelView>("Trace", this, canClose: false, keepAlive: true, key: "Steps");
        var description = DocumentViewModel.Create<TraceDescriptionPanelView>("Description", this, keepAlive: true, key: "Description");
        var results = DocumentViewModel.Create<TraceResultsPanelView>("Results", this, keepAlive: true, key: "Results");
        var strategy = DocumentViewModel.Create<TraceStrategyPanelView>("Strategy", this, keepAlive: true, key: "Strategy");

        _resultsDocument = results;

        var details = new TabGroupNode(description, results, strategy);

        var right = new SplitNode(Orientation.Vertical, new TabGroupNode(steps), details);

        return new DockLayoutViewModel(new SplitNode(Orientation.Horizontal, new TabGroupNode(visual), right));
    }

    public TraceKind Kind { get; }

    private IStepService StepService { get; }

    private IndexService IndexService { get; }

    public DatabaseSource Database { get; }

    public AllocationUnit AllocationUnit { get; }

    [ObservableProperty]
    private PlanNode? _planNode;

    private DateTime? QueryTime { get; set; }

    [ObservableProperty]
    private ScanModeResult? _scanMode;

    public void Refresh(PlanNode planNode, DateTime? queryTime, ScanModeResult? scanMode)
    {
        PlanNode = planNode;
        QueryTime = queryTime;
        ScanMode = scanMode;

        ResetStep();

        OnPropertyChanged(nameof(SeekDescription));
        OnPropertyChanged(nameof(IconSource));
    }

    public event EventHandler<PageNavigatedEventArgs>? PageNavigated;

    private bool _hasNavigatedSinceReset;

    [ObservableProperty]
    private ObservableCollection<AccessStep> _stepHistory = [];

    [ObservableProperty]
    private ObservableCollection<IndexRecordModel> _resultRecords = [];

    [ObservableProperty]
    private AccessStep? _currentStep;

    [ObservableProperty]
    private AccessStrategy? _strategy;

    [ObservableProperty]
    private bool _isStepping;

    [ObservableProperty]
    private bool _isStepComplete;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isRunningToEnd;

    [ObservableProperty]
    private List<IndexNode> _nodes = [];

    [ObservableProperty]
    private bool _isVisualInitialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _loadedPageCount;

    [ObservableProperty]
    private float _zoom = 1;

    [ObservableProperty]
    private bool _isZoomToFit = true;

    [ObservableProperty]
    private IReadOnlyList<PageSpan> _pageSpans = [];

    [ObservableProperty]
    private long _playheadTimeUs;

    [ObservableProperty]
    private PageAddress? _selectedPageAddress;

    [ObservableProperty]
    private int? _selectedSlot;

    public long TotalPageCount => AllocationUnit.UsedPages;

    public string ProgressText => TotalPageCount > 0 ? $"{LoadedPageCount:N0} of {TotalPageCount:N0} pages" : string.Empty;

    public bool IsProgressVisible => TotalPageCount >= IndexService.ProgressReportInterval;

    public double ProgressMaximum => TotalPageCount;

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _allocationLayers = [];

    [ObservableProperty]
    private IReadOnlyList<AllocationBorder> _traceBorders = [];

    [ObservableProperty]
    private RowIdentifier? _selectedRowIdentifier;

    [ObservableProperty]
    private int _selectedRowSlotCount;

    private PageAddress? _currentTracePage;

    private AllocationBorder? _objectBorder;

    private bool _objectBorderVisible;

    private readonly List<PageSpan> _visitedPages = [];

    private DocumentViewModel? _resultsDocument;

    private CancellationTokenSource? _runToEndCancellation;

    [ObservableProperty]
    private bool _isResultsVisible = true;

    private bool _suppressResultsSync;

    public string ResultsLabel => ResultRecords.Count > 0 ? $"Results ({ResultRecords.Count:N0})" : "Results";

    partial void OnIsResultsVisibleChanged(bool value)
    {
        if (_suppressResultsSync || _resultsDocument is not { } document)
        {
            return;
        }

        if (value)
        {
            Dock.Show(document);
        }
        else
        {
            Dock.Close(document);
        }
    }

    private void OnDockLayoutChanged()
    {
        if (_resultsDocument is not { } document)
        {
            return;
        }

        _suppressResultsSync = true;

        IsResultsVisible = Dock.Contains(document);

        _suppressResultsSync = false;
    }

    private void UpdateResultsTitle()
    {
        OnPropertyChanged(nameof(ResultsLabel));
    }

    public short VisualFileId
        => (AllocationUnit.FirstPage != PageAddress.Empty ? AllocationUnit.FirstPage : AllocationUnit.FirstIamPage).FileId;

    public int ExtentCount => Database.GetFilePageCount(VisualFileId) / 8;

    public PfsChain? PfsChain => Database.Pfs.GetValueOrDefault(VisualFileId);

    public async Task LoadVisualAsync()
    {
        if (IsVisualInitialized)
        {
            return;
        }

        if (Kind == TraceKind.Allocation)
        {
            var layers = await Task.Run(() => AllocationLayerBuilder.GenerateLayers(Database, true, 20));

            var traceName = string.IsNullOrEmpty(AllocationUnit.IndexName)
                ? $"{AllocationUnit.SchemaName}.{AllocationUnit.TableName}"
                : $"{AllocationUnit.SchemaName}.{AllocationUnit.TableName}.{AllocationUnit.IndexName}";

            foreach (var layer in layers.Where(l => !l.IsAllocationLayer))
            {
                layer.Opacity = layer.Name == traceName ? (byte)80 : (byte)5;
            }

            AllocationLayers = new ObservableCollection<AllocationLayer>(layers);

            var iamPageIds = AllocationUnit.IamChain
                                           .Pages
                                           .Where(p => p.PageAddress.FileId == VisualFileId)
                                           .Select(p => p.PageAddress.PageId)
                                           .ToHashSet();

            var ranges = AllocationUnit.IamChain
                                       .GetAllocatedPageRanges(VisualFileId)
                                       .Where(r => !(r.From == r.To && iamPageIds.Contains(r.From)))
                                       .Select(r => new TimedRange(r.From, r.To, 0, long.MaxValue))
                                       .ToList();

            _objectBorder = new AllocationBorder(AllocationBorderScope.Page, VisualFileId, Color.DimGray, ranges);

            IsVisualInitialized = true;

            return;
        }

        LoadedPageCount = 0;

        IndexService.ProgressReportInterval = TotalPageCount > 100_000 ? 4096 : 1;

        OnPropertyChanged(nameof(IsProgressVisible));

        var progress = new Progress<int>(count => LoadedPageCount = count);

        Nodes = await Task.Run(() => IndexService.GetNodes(Database, AllocationUnit.RootPage, CancellationToken.None, progress));

        IsVisualInitialized = true;
    }

    public AccessPhase? CurrentPhase => CurrentStep?.AccessPhase;

    public AccessCounters CurrentCounters => CurrentStep?.Counters ?? default;

    public bool IsWalkInProgress => IsStepping && !IsStepComplete;

    public AccessStrategy? SeekDescription
    {
        get
        {
            var predicateInfo = PlanNode?.PredicateInfo;

            if (Kind == TraceKind.Allocation)
            {
                return AccessStrategyBuilder.BuildAllocationScan(predicateInfo?.Residual,
                                                                 predicateInfo?.RowGoal,
                                                                 hasUntranslatedResidual: predicateInfo?.HasUntranslatedPredicate == true) with
                {
                    EntryPoint = AllocationUnit.FirstIamPage,
                    EntryPointSource = "sys.sysallocunits.pgfirstiam"
                };
            }

            var indexStructure = IndexStructureProvider.GetIndexStructure(Database, AllocationUnit.AllocationUnitId);

            IReadOnlyList<SeekBounds> ranges = predicateInfo is { HasSeekBounds: true }
                                                ? predicateInfo.SeekBounds
                                                : [SeekBounds.All];

            return AccessStrategyBuilder.Build(indexStructure,
                                               ranges[0],
                                               ScanDirection,
                                               predicateInfo?.RowGoal,
                                               predicateInfo?.Residual,
                                               ranges: ranges,
                                               hasUntranslatedResidual: predicateInfo?.HasUntranslatedPredicate == true) with
            {
                EntryPoint = AllocationUnit.RootPage,
                EntryPointSource = "sys.sysallocunits.pgroot"
            };
        }
    }

    private ScanDirection ScanDirection
        => PlanNode?.ScanInfo?.IsForward == false ? ScanDirection.Backward : ScanDirection.Forward;

    partial void OnCurrentStepChanged(AccessStep? value)
    {
        OnPropertyChanged(nameof(CurrentPhase));
        OnPropertyChanged(nameof(CurrentCounters));
    }

    partial void OnIsSteppingChanged(bool value) => OnPropertyChanged(nameof(IsWalkInProgress));

    partial void OnIsStepCompleteChanged(bool value) => OnPropertyChanged(nameof(IsWalkInProgress));

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task Run()
    {
        if (IsRunning)
        {
            IsRunning = false;

            return;
        }

        IsRunning = true;

        while (IsRunning && !IsStepComplete)
        {
            await StepNext();

            await Task.Delay(CurrentStep?.AccessPhase == AccessPhase.Walk ? RunStepDelayMs / 10 : RunStepDelayMs);
        }

        IsRunning = false;
    }

    [RelayCommand]
    public async Task StepNext()
    {
        if (!IsStepping)
        {
            await StartAsync();
        }

        var step = await Task.Run(() => StepService.StepNextAsync(CancellationToken.None));

        if (step is null)
        {
            IsStepComplete = true;
            IsRunning = false;

            return;
        }

        Append(step, StepHistory);

        if (step is AccessStep.Row { EmittedRecord: { } emitted })
        {
            ResultRecords.Add(ToRecordModel(emitted));

            UpdateResultsTitle();
        }

        UpdateVisualPosition(step);

        CurrentStep = step;

        var readPage = step switch
        {
            AccessStep.ReadPage read => read.PageAddress,
            AccessStep.IamRead iam => iam.PageAddress,
            AccessStep.PfsRead pfs => pfs.PageAddress,
            _ => (PageAddress?)null
        };

        if (readPage is { } pageAddress)
        {
            PageNavigated?.Invoke(this, new PageNavigatedEventArgs(pageAddress, !_hasNavigatedSinceReset)
            {
                Selection = Kind == TraceKind.Allocation ? PageReadSelection.Last : PageReadSelection.Next
            });

            _hasNavigatedSinceReset = true;
        }

        if (step is AccessStep.Stopped)
        {
            IsStepComplete = true;
            IsRunning = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task RunToEnd()
    {
        if (IsRunningToEnd)
        {
            _runToEndCancellation?.Cancel();

            return;
        }

        IsRunningToEnd = true;

        _runToEndCancellation = new CancellationTokenSource();

        var cancellationToken = _runToEndCancellation.Token;

        try
        {
            if (!IsStepping)
            {
                await StartAsync();
            }

            var steps = new ObservableCollection<AccessStep>();

            var results = new ObservableCollection<IndexRecordModel>();

            var visited = new List<PageSpan>();

            PageAddress? lastPage = null;

            PageAddress? lastDataPage = null;

            int? lastSlot = null;

            var lastSlotCount = 0;

            await Task.Run(async () =>
            {
                try
                {
                    while (await StepService.StepNextAsync(cancellationToken) is not null)
                    {
                    }
                }
                catch (OperationCanceledException)
                {
                }

                foreach (var step in StepService.History)
                {
                    Append(step, steps);

                    if (step is AccessStep.Row { EmittedRecord: { } emitted })
                    {
                        results.Add(ToRecordModel(emitted));
                    }

                    switch (step)
                    {
                        case AccessStep.ReadPage read:
                            visited.Add(new PageSpan(read.PageAddress, 0, long.MaxValue));
                            lastPage = read.PageAddress;
                            lastDataPage = read.PageAddress;
                            lastSlotCount = read.SlotCount;
                            lastSlot = null;
                            break;

                        case AccessStep.PageSkipped skipped when Kind == TraceKind.Allocation:
                            lastPage = skipped.PageAddress;
                            break;

                        case AccessStep.PfsRead pfsRead when Kind == TraceKind.Allocation:
                            lastPage = pfsRead.PageAddress;
                            break;

                        default:
                            lastSlot = GetStepSlot(step) ?? lastSlot;
                            break;
                    }
                }
            }, CancellationToken.None);

            StepHistory = steps;
            ResultRecords = results;

            UpdateResultsTitle();

            if (Kind == TraceKind.Index)
            {
                _visitedPages.Clear();
                _visitedPages.AddRange(visited);

                PageSpans = [.. _visitedPages];
                SelectedPageAddress = lastPage;
                SelectedSlot = lastSlot;
            }
            else if (lastPage is { } page)
            {
                _objectBorderVisible = true;

                SetTraceBorders(page);

                _currentTracePage = lastDataPage;
                SelectedRowSlotCount = lastSlotCount;
                SelectedRowIdentifier = lastDataPage is { } dataPage && lastSlot is { } slot
                    ? new RowIdentifier(dataPage, (ushort)slot)
                    : null;
            }

            CurrentStep = StepService.Current;
            IsStepComplete = StepService.IsComplete;
        }
        finally
        {
            IsRunningToEnd = false;

            _runToEndCancellation.Dispose();
            _runToEndCancellation = null;
        }
    }

    [RelayCommand]
    public void ResetStep()
    {
        _runToEndCancellation?.Cancel();

        IsRunning = false;
        IsRunningToEnd = false;
        IsStepping = false;
        IsStepComplete = false;

        _hasNavigatedSinceReset = false;

        StepHistory = [];
        ResultRecords = [];
        CurrentStep = null;
        Strategy = SeekDescription;

        _visitedPages.Clear();
        _currentTracePage = null;
        _objectBorderVisible = false;
        PageSpans = [];
        SelectedPageAddress = null;
        SelectedSlot = null;
        SelectedRowIdentifier = null;
        SelectedRowSlotCount = 0;
        TraceBorders = [];

        UpdateResultsTitle();
    }

    private static int? GetStepSlot(AccessStep step)
    {
        return step switch
        {
            AccessStep.ReadPage => null,
            AccessStep.Probe probe => probe.Middle,
            AccessStep.ProbeResult probeResult => probeResult.Slot,
            AccessStep.Row row => row.Slot,
            AccessStep.RowRun run => run.ToSlot,
            AccessStep.RangeEnd rangeEnd => rangeEnd.Slot,
            AccessStep.Descend descend => descend.Slot,
            _ => null
        };
    }

    private void UpdateVisualPosition(AccessStep step)
    {
        if (Kind == TraceKind.Index)
        {
            if (step is AccessStep.ReadPage read)
            {
                SelectedPageAddress = read.PageAddress;

                SelectedSlot = null;

                _visitedPages.Add(new PageSpan(read.PageAddress, 0, long.MaxValue));

                PageSpans = [.. _visitedPages];
            }
            else
            {
                SelectedSlot = GetStepSlot(step) ?? SelectedSlot;
            }

            return;
        }

        switch (step)
        {
            case AccessStep.ReadPage read:
                _currentTracePage = read.PageAddress;
                SelectedRowSlotCount = read.SlotCount;
                SelectedRowIdentifier = null;
                break;

            case AccessStep.Row row when _currentTracePage is { } rowPage:
                SelectedRowIdentifier = new RowIdentifier(rowPage, (ushort)row.Slot);
                break;

            case AccessStep.RowRun run when _currentTracePage is { } runPage:
                SelectedRowIdentifier = new RowIdentifier(runPage, (ushort)run.ToSlot);
                break;

            case AccessStep.IamRead:
                _objectBorderVisible = true;
                SelectedRowIdentifier = null;
                TraceBorders = _objectBorder is { } revealed ? [revealed] : [];
                break;

            default:
                SelectedRowIdentifier = null;
                break;
        }

        var current = step switch
        {
            AccessStep.ReadPage readPage => readPage.PageAddress,
            AccessStep.PageSkipped skipped => skipped.PageAddress,
            AccessStep.PfsRead pfsRead => pfsRead.PageAddress,
            _ => (PageAddress?)null
        };

        if (current is { } page)
        {
            SetTraceBorders(page);
        }
    }

    private void SetTraceBorders(PageAddress page)
    {
        var currentBorder = new AllocationBorder(AllocationBorderScope.Page,
                                                 page.FileId,
                                                 Color.Red,
                                                 [new TimedRange(page.PageId, page.PageId, 0, long.MaxValue)]);

        TraceBorders = _objectBorderVisible && _objectBorder is { } border ? [border, currentBorder] : [currentBorder];
    }

    private async Task StartAsync()
    {
        var predicateInfo = PlanNode?.PredicateInfo;

        var evaluationContext = QueryTime is { } queryTime ? new EvaluationContext(queryTime) : null;

        var hasUntranslated = predicateInfo?.HasUntranslatedPredicate == true;

        if (Kind == TraceKind.Allocation)
        {
            var service = (AllocationStepService)StepService;

            await Task.Run(() => service.StartAsync(Database,
                                                    AllocationUnit.FirstIamPage,
                                                    predicateInfo?.Residual,
                                                    CancellationToken.None,
                                                    predicateInfo?.RowGoal,
                                                    evaluationContext,
                                                    hasUntranslated));
        }
        else
        {
            var service = (IndexStepService)StepService;

            IReadOnlyList<SeekBounds> ranges = predicateInfo is { HasSeekBounds: true }
                ? predicateInfo.SeekBounds
                : [SeekBounds.All];

            var residual = PlanNode?.HasRedundantResidual() == true ? null : predicateInfo?.Residual;

            await Task.Run(() => service.StartAsync(Database,
                                                    AllocationUnit.AllocationUnitId,
                                                    AllocationUnit.RootPage,
                                                    ranges,
                                                    residual,
                                                    ScanDirection,
                                                    CancellationToken.None,
                                                    predicateInfo?.RowGoal,
                                                    hasUntranslated,
                                                    evaluationContext));
        }

        Strategy = StepService.Strategy;
        IsStepping = true;
    }

    private static IndexRecordModel ToRecordModel(IRecord record)
    {
        return new IndexRecordModel
        {
            Slot = record.Slot,
            Fields =
            [
                .. record.Fields.Select(f => new IndexRecordFieldModel
                {
                    Name = f.Name,
                    Value = f.Value,
                    DataType = f.ColumnStructure.DataType
                })
            ]
        };
    }

    private static void Append(AccessStep step, ObservableCollection<AccessStep> history)
    {
        if (step is AccessStep.Row row && history.Count > 0)
        {
            var latest = history[0];

            if (latest is AccessStep.Row previous && previous.Outcome == row.Outcome && Math.Abs(row.Slot - previous.Slot) == 1)
            {
                history[0] = new AccessStep.RowRun(previous.Slot, row.Slot, row.Outcome)
                {
                    Count = 2,
                    HasResidual = row.HasResidual,
                    HasRange = row.HasRange,
                    EmitCount = EmitOf(previous) + EmitOf(row),
                    Counters = row.Counters
                };

                return;
            }

            if (latest is AccessStep.RowRun run && run.Outcome == row.Outcome && Math.Abs(row.Slot - run.ToSlot) == 1)
            {
                history[0] = run with
                {
                    ToSlot = row.Slot,
                    Count = run.Count + 1,
                    EmitCount = run.EmitCount + EmitOf(row),
                    Counters = row.Counters
                };

                return;
            }
        }

        history.Insert(0, step);

        if (history.Count > HistoryLimit)
        {
            history.RemoveAt(history.Count - 1);
        }
    }

    private static int EmitOf(AccessStep.Row row)
    {
        return row.Outcome == RowOutcome.Match ? 1 : 0;
    }
}
