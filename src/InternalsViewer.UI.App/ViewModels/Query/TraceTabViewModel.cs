using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.Interfaces.Services.Joins;
using InternalsViewer.Execution.Services.Joins.Inputs;
using System.Collections.Generic;
using System.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Services.Allocations;
using InternalsViewer.Execution.Services.Heaps;
using InternalsViewer.Execution.Services.Indexes;
using InternalsViewer.Execution.Services.Joins;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Joins;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Trace;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.Views.Query.Tabs.Trace;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.Execution.AccessPaths.Joins;

namespace InternalsViewer.UI.App.ViewModels.Query;

public enum TraceKind
{
    Index,
    Allocation,
    KeyLookup,
    RidLookup,
    MergeJoin,
    HashMatch
}

public sealed class TraceTabViewModelFactory(IPageService pageService,
                                             IRecordService recordService,
                                             IndexService indexService)
{
    public TraceTabViewModel Create(TraceKind kind,
                                    DatabaseSource database,
                                    AllocationUnit allocationUnit,
                                    PlanNode? planNode,
                                    DateTime? queryTime,
                                    ScanModeResult? scanMode)
    {
        IStepService service = kind == TraceKind.Index
            ? new IndexStepService(pageService, recordService)
            : new AllocationStepService(pageService, recordService);

        var visualKind = kind == TraceKind.Index ? TraceVisualKind.Index : TraceVisualKind.Allocation;

        var visualTitle = kind == TraceKind.Index ? "Index" : "Allocations";

        var visuals = new[] { new TraceVisualViewModel(visualKind, database, allocationUnit, indexService, visualTitle) };

        return new TraceTabViewModel(kind, service, database, allocationUnit, planNode, queryTime, scanMode, visuals);
    }

    public TraceTabViewModel CreateKeyLookup(DatabaseSource database,
                                             AllocationUnit outerUnit,
                                             AllocationUnit innerUnit,
                                             PlanNode joinNode,
                                             DateTime? queryTime)
    {
        var service = new NestedLoopsStepService(new IndexStepService(pageService, recordService),
                                                 new IndexStepService(pageService, recordService));

        var visuals = new[]
        {
            new TraceVisualViewModel(TraceVisualKind.Index,
                                     database,
                                     outerUnit,
                                     indexService,
                                     $"Seek: {outerUnit.IndexName}",
                                     NestedLoopsStepService.OuterSource) { IsSideStackVisible = true },
            new TraceVisualViewModel(TraceVisualKind.Index,
                                     database,
                                     innerUnit,
                                     indexService,
                                     $"Lookup: {innerUnit.IndexName}",
                                     NestedLoopsStepService.InnerSource) { IsSideStackVisible = true }
        };

        return new TraceTabViewModel(TraceKind.KeyLookup, service, database, outerUnit, joinNode, queryTime, null, visuals);
    }

    public TraceTabViewModel CreateRidLookup(DatabaseSource database,
                                             AllocationUnit outerUnit,
                                             AllocationUnit heapUnit,
                                             PlanNode joinNode,
                                             DateTime? queryTime)
    {
        var service = new NestedLoopsStepService(new IndexStepService(pageService, recordService),
                                                 new IndexStepService(pageService, recordService));

        var visuals = new[]
        {
            new TraceVisualViewModel(TraceVisualKind.Index,
                                     database,
                                     outerUnit,
                                     indexService,
                                     $"Seek: {outerUnit.IndexName}",
                                     NestedLoopsStepService.OuterSource) { IsSideStackVisible = true },
            new TraceVisualViewModel(TraceVisualKind.Allocation,
                                     database,
                                     heapUnit,
                                     indexService,
                                     $"Heap: {heapUnit.TableName}",
                                     NestedLoopsStepService.InnerSource)
            {
                IsSideStackVisible = true,
                ShowObjectBorderImmediately = true
            }
        };

        return new TraceTabViewModel(TraceKind.RidLookup,
                                     service,
                                     database,
                                     outerUnit,
                                     joinNode,
                                     queryTime,
                                     null,
                                     visuals,
                                     new HeapFetchStepService(pageService, recordService));
    }

    public TraceTabViewModel CreateMergeJoin(DatabaseSource database,
                                             AllocationUnit outerUnit,
                                             AllocationUnit innerUnit,
                                             PlanNode joinNode,
                                             DateTime? queryTime)
    {
        var service = new MergeJoinStepService(new IndexStepService(pageService, recordService),
                                               new IndexStepService(pageService, recordService));

        var visuals = new[]
        {
            new TraceVisualViewModel(TraceVisualKind.Index,
                                     database,
                                     outerUnit,
                                     indexService,
                                     $"Outer: {DisplayName(outerUnit)}",
                                     MergeJoinStepService.OuterSource) { IsSideStackVisible = true },
            new TraceVisualViewModel(TraceVisualKind.Index,
                                     database,
                                     innerUnit,
                                     indexService,
                                     $"Inner: {DisplayName(innerUnit)}",
                                     MergeJoinStepService.InnerSource) { IsSideStackVisible = true }
        };

        return new TraceTabViewModel(TraceKind.MergeJoin, service, database, outerUnit, joinNode, queryTime, null, visuals);
    }

    public TraceTabViewModel CreateHashMatch(DatabaseSource database,
                                             AllocationUnit buildUnit,
                                             AllocationUnit probeUnit,
                                             PlanNode joinNode,
                                             DateTime? queryTime)
    {
        var service = new HashMatchStepService(new IndexStepService(pageService, recordService),
                                               new IndexStepService(pageService, recordService));

        var visuals = new[]
        {
            new TraceVisualViewModel(TraceVisualKind.Index,
                                     database,
                                     buildUnit,
                                     indexService,
                                     $"Build: {DisplayName(buildUnit)}",
                                     HashMatchStepService.BuildSource)
            {
                IsSideStackVisible = true,
                IsHashTableVisible = true
            },
            new TraceVisualViewModel(TraceVisualKind.Index,
                                     database,
                                     probeUnit,
                                     indexService,
                                     $"Probe: {DisplayName(probeUnit)}",
                                     HashMatchStepService.ProbeSource) { IsSideStackVisible = true }
        };

        return new TraceTabViewModel(TraceKind.HashMatch, service, database, buildUnit, joinNode, queryTime, null, visuals);
    }

    private static string DisplayName(AllocationUnit unit)
        => string.IsNullOrEmpty(unit.IndexName) ? unit.TableName ?? string.Empty : unit.IndexName;
}

public sealed partial class TraceTabViewModel : ObservableObject
{
    private const int RunStepDelayMs = 150;

    private const int HistoryLimit = 1000;

    public TraceTabViewModel(TraceKind kind,
                             IStepService stepService,
                             DatabaseSource database,
                             AllocationUnit allocationUnit,
                             PlanNode? planNode,
                             DateTime? queryTime,
                             ScanModeResult? scanMode,
                             IReadOnlyList<TraceVisualViewModel> visuals,
                             HeapFetchStepService? heapService = null)
    {
        HeapService = heapService;
        Kind = kind;
        StepService = stepService;
        Database = database;
        AllocationUnit = allocationUnit;
        PlanNode = planNode;
        QueryTime = queryTime;
        ScanMode = scanMode;
        Visuals = visuals;

        if (Visuals.FirstOrDefault(v => v.IsHashTableVisible) is { } buildVisual)
        {
            buildVisual.PropertyChanged += OnBuildVisualPropertyChanged;
        }

        Dock = BuildDock();

        Dock.LayoutChanged += (_, _) => OnDockLayoutChanged();

        Strategy = SeekDescription;
    }

    public SvgImageSource? IconSource => PlanNode is null ? null : new SvgImageSource(PlanIconResolver.Resolve(PlanNode));

    public DockLayoutViewModel Dock { get; }

    private DockLayoutViewModel BuildDock()
    {
        var visualDocuments = Visuals.Select(v => DocumentViewModel.Create<TraceVisualPanelView>(v.Title,
                                                                                                 v,
                                                                                                 canClose: false,
                                                                                                 keepAlive: true,
                                                                                                 key: $"Visual{v.Source}"))
                                     .ToArray();

        var steps = DocumentViewModel.Create<TraceStepsPanelView>("Trace", this, canClose: false, keepAlive: true, key: "Steps");
        var description = DocumentViewModel.Create<TraceDescriptionPanelView>("Description", this, keepAlive: true, key: "Description");
        var results = DocumentViewModel.Create<TraceResultsPanelView>("Results", this, keepAlive: true, key: "Results");
        var strategy = DocumentViewModel.Create<TraceStrategyPanelView>("Strategy", this, keepAlive: true, key: "Strategy");

        _resultsDocument = results;

        var right = new TabGroupNode(steps, description, results, strategy);

        // Each side of a join gets its own pane so both walks are visible at once, rather than tabs hiding one behind the other
        LayoutNode visualNode = visualDocuments.Length > 1
            ? visualDocuments.Skip(1)
                             .Aggregate((LayoutNode)new TabGroupNode(visualDocuments[0]),
                                        (left, document) => new SplitNode(Orientation.Horizontal, left, new TabGroupNode(document)))
            : new TabGroupNode(visualDocuments);

        return new DockLayoutViewModel(new SplitNode(Orientation.Horizontal, visualNode, right));
    }

    public TraceKind Kind { get; }

    public IReadOnlyList<TraceVisualViewModel> Visuals { get; }

    private HeapFetchStepService? HeapService { get; }

    private IStepService StepService { get; }

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
        OnPropertyChanged(nameof(JoinRule));
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
    private AccessStrategy? _innerStrategy;

    public string OuterSectionTitle => Visuals.Count > 0 ? Visuals[0].Title : string.Empty;

    public string InnerSectionTitle => Visuals.Count > 1 ? Visuals[1].Title : string.Empty;

    public Brush? OuterAccentBrush => Visuals.Count > 1 ? ToBrush(Visuals[0].ObjectColour) : null;

    public Brush? InnerAccentBrush => Visuals.Count > 1 ? ToBrush(Visuals[1].ObjectColour) : null;

    private static Brush ToBrush(System.Drawing.Color colour)
    {
        return new SolidColorBrush(Windows.UI.Color.FromArgb(colour.A, colour.R, colour.G, colour.B));
    }

    [ObservableProperty]
    private bool _isStepping;

    [ObservableProperty]
    private bool _isStepComplete;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isRunningToEnd;

    [ObservableProperty]
    private long _playheadTimeUs;

    partial void OnPlayheadTimeUsChanged(long value)
    {
        foreach (var visual in Visuals)
        {
            visual.PlayheadTimeUs = value;
        }
    }

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

    public AccessPhase? CurrentPhase => CurrentStep?.AccessPhase;

    public AccessPhase? OuterPhase
        => CurrentStep is { } step && step.Source == NestedLoopsStepService.OuterSource ? step.AccessPhase : null;

    public AccessPhase? InnerPhase
        => CurrentStep is { } step && step.Source == NestedLoopsStepService.InnerSource ? step.AccessPhase : null;

    public AccessCounters CurrentCounters => CurrentStep?.Counters ?? default;

    public bool IsWalkInProgress => IsStepping && !IsStepComplete;

    public AccessStrategy? SeekDescription
    {
        get
        {
            if (Kind is TraceKind.KeyLookup or TraceKind.RidLookup or TraceKind.MergeJoin or TraceKind.HashMatch)
            {
                if (ResolveSides() is not { } sides)
                {
                    return null;
                }

                var outerPredicate = sides.Outer.PredicateInfo;

                var outerUnit = Visuals[0].AllocationUnit;

                var outerStructure = IndexStructureProvider.GetIndexStructure(Database, outerUnit.AllocationUnitId);

                IReadOnlyList<SeekBounds> outerRanges = outerPredicate is { HasSeekBounds: true }
                                                        ? outerPredicate.SeekBounds
                                                        : [SeekBounds.All];

                return AccessStrategyBuilder.Build(outerStructure,
                                                   outerRanges[0],
                                                   SideScanDirection(sides.Outer),
                                                   outerPredicate?.RowGoal,
                                                   outerPredicate?.Residual,
                                                   ranges: outerRanges,
                                                   hasUntranslatedResidual: outerPredicate?.HasUntranslatedPredicate == true) with
                {
                    EntryPoint = outerUnit.RootPage,
                    EntryPointSource = "sys.sysallocunits.pgroot"
                };
            }

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

    private CorrelatedJoin? ResolveJoin()
    {
        return PlanNode is null ? null : CorrelatedJoinResolver.Resolve(PlanNode);
    }

    /// <summary>
    /// What this join requires of each side, for the rule shown alongside the operator
    /// </summary>
    public JoinDecision? JoinRule
    {
        get
        {
            if (PlanNode is null)
            {
                return null;
            }

            JoinType? joinType = Kind switch
            {
                TraceKind.KeyLookup or TraceKind.RidLookup => CorrelatedJoinResolver.Resolve(PlanNode)?.JoinType,
                TraceKind.MergeJoin => MergeJoinResolver.Resolve(PlanNode)?.JoinType,
                TraceKind.HashMatch => HashJoinResolver.Resolve(PlanNode)?.JoinType,
                _ => null
            };

            return joinType?.Decide(true, true);
        }
    }

    private (PlanNode Outer, PlanNode Inner)? ResolveSides()
    {
        if (PlanNode is null)
        {
            return null;
        }

        if (Kind is TraceKind.KeyLookup or TraceKind.RidLookup)
        {
            return CorrelatedJoinResolver.Resolve(PlanNode) is { } correlated ? (correlated.Outer, correlated.Inner) : null;
        }

        if (Kind == TraceKind.MergeJoin)
        {
            return MergeJoinResolver.Resolve(PlanNode) is { } merge ? (merge.Outer, merge.Inner) : null;
        }

        if (Kind == TraceKind.HashMatch)
        {
            return HashJoinResolver.Resolve(PlanNode) is { } hash ? (hash.Build, hash.Probe) : null;
        }

        return null;
    }

    private static ScanDirection SideScanDirection(PlanNode side)
        => side.ScanInfo?.IsForward == false ? ScanDirection.Backward : ScanDirection.Forward;

    partial void OnCurrentStepChanged(AccessStep? value)
    {
        OnPropertyChanged(nameof(CurrentPhase));
        OnPropertyChanged(nameof(OuterPhase));
        OnPropertyChanged(nameof(InnerPhase));
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

        if (ToResultModel(step) is { } resultModel)
        {
            ResultRecords.Add(resultModel);

            UpdateResultsTitle();
        }

        UpdateInnerStrategy();

        SyncSideBuffers();

        SyncHashTable(step);

        GetVisual(step.Source)?.Apply(step);

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

        UpdateActiveVisual(step);
    }

    private void UpdateActiveVisual(AccessStep? step)
    {
        var isSideStep = step is not null && Visuals.Any(v => v.Source == step.Source);

        foreach (var visual in Visuals)
        {
            visual.IsDimmed = Visuals.Count > 1
                              && isSideStep
                              && !IsStepComplete
                              && !IsRunning
                              && !IsRunningToEnd
                              && visual.Source != step!.Source;
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

            var replays = new Dictionary<TraceVisualViewModel, TraceVisualReplay>();

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

                    if (ToResultModel(step) is { } resultModel)
                    {
                        results.Add(resultModel);
                    }
                }

                foreach (var visual in Visuals)
                {
                    replays[visual] = visual.ComputeReplay(StepService.History);
                }
            }, CancellationToken.None);

            StepHistory = steps;
            ResultRecords = results;

            UpdateResultsTitle();
            UpdateInnerStrategy();

            foreach (var visual in Visuals)
            {
                visual.ApplyReplay(replays[visual]);
            }

            SyncSideBuffers();

            SyncHashTable(StepService.Current);

            CurrentStep = StepService.Current;
            IsStepComplete = StepService.IsComplete;

            UpdateActiveVisual(CurrentStep);
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

        _hashBucketModels = null;
        _syncedHashRowCount = 0;
        _currentHashBucket = null;
        _currentHashEntry = null;
        _hashColumns = null;

        _matchedHashEntries.Clear();
        CurrentStep = null;
        Strategy = SeekDescription;
        InnerStrategy = null;

        _syncedBuffers.Clear();

        foreach (var visual in Visuals)
        {
            visual.Reset();
        }

        UpdateResultsTitle();
    }

    private TraceVisualViewModel? GetVisual(int source)
    {
        return Visuals.FirstOrDefault(v => v.Source == source);
    }

    /// <summary>
    /// Refreshes each side's row pane from the rows the join is currently holding
    /// </summary>
    /// <remarks>
    /// Taken from the join rather than accumulated from the row steps, because only the join knows which rows it still holds - a row it
    /// has advanced past is gone, and a row read ahead of the current key is not yet in play.
    /// </remarks>
    private void SyncSideBuffers()
    {
        if (StepService is not IJoinStepService join)
        {
            return;
        }

        foreach (var visual in Visuals)
        {
            if (!visual.IsSideStackVisible || visual.IsHashTableVisible)
            {
                continue;
            }

            var buffer = visual.Source == NestedLoopsStepService.OuterSource ? join.Outer.Buffer : join.Inner.Buffer;

            if (_syncedBuffers.TryGetValue(visual.Source, out var synced) && synced.SequenceEqual(buffer))
            {
                continue;
            }

            _syncedBuffers[visual.Source] = [.. buffer];

            visual.SideRecords = [.. buffer.Select(ToRecordModel)];
        }
    }

    private readonly Dictionary<int, List<JoinBufferRow>> _syncedBuffers = new();

    private void OnBuildVisualPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TraceVisualViewModel.HashBucketCount)
            || sender is not TraceVisualViewModel visual
            || StepService is not HashMatchStepService hashService)
        {
            return;
        }

        hashService.SetBucketCount(visual.HashBucketCount);

        SyncHashTable(CurrentStep);
    }

    private void SyncHashTable(AccessStep? step)
    {
        if (StepService is not HashMatchStepService hashService)
        {
            return;
        }

        var visual = Visuals.FirstOrDefault(v => v.IsHashTableVisible);

        if (visual is null)
        {
            return;
        }

        var table = hashService.Table;

        if (_hashColumns is null && FirstRecord(table) is { } sample)
        {
            _hashColumns = BuildHashColumns(sample);

            visual.HashColumns = _hashColumns;

            _hashBucketModels = null;
        }

        if (_hashBucketModels is { } models
            && models.Count == table.BucketCount
            && table.RowCount == _syncedHashRowCount + 1
            && step is AccessStep.HashBuild { IsNullKey: false } build)
        {
            models[build.Bucket].Entries.Add(ToEntryModel(build.Bucket,
                                                          build.Entry,
                                                          table.Buckets[build.Bucket].Entries[build.Entry]));

            _syncedHashRowCount = table.RowCount;
        }
        else if (_hashBucketModels is null
                 || _hashBucketModels.Count != table.BucketCount
                 || _syncedHashRowCount != table.RowCount)
        {
            RebuildHashBuckets(table);

            visual.HashBuckets = _hashBucketModels!;
        }

        UpdateHashHighlight(step);

        visual.HashTableSummary = hashService.BuildRowEstimate > 0
            ? $"{table.RowCount:N0} rows, sized for {hashService.BuildRowEstimate:N0}, "
              + $"{table.BucketCount} buckets, longest chain {table.LongestChain}"
            : $"{table.RowCount:N0} rows, {table.BucketCount} buckets, longest chain {table.LongestChain}";
    }

    private List<HashBucketModel>? _hashBucketModels;

    private int _syncedHashRowCount;

    private HashBucketModel? _currentHashBucket;

    private HashEntryModel? _currentHashEntry;

    private IReadOnlyList<HashColumnModel>? _hashColumns;

    private static IRecord? FirstRecord(HashTable table)
    {
        foreach (var bucket in table.Buckets)
        {
            if (bucket.Count > 0)
            {
                return bucket.Entries[0].Record;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds the grid's columns from the shape of a build row, sizing each to its header and first value
    /// </summary>
    private static List<HashColumnModel> BuildHashColumns(IRecord record)
    {
        var columns = new List<HashColumnModel>(HashColumnModel.CreateBaseColumns());

        var sample = ToRecordModel(record);

        foreach (var field in sample.Fields)
        {
            columns.Add(new HashColumnModel
            {
                Header = field.Name,
                Width = Math.Clamp(Math.Max(field.Name.Length, field.Value.Length) * 7.5 + 18, 70, 240)
            });
        }

        return columns;
    }

    private readonly List<HashEntryModel> _matchedHashEntries = [];

    private void RebuildHashBuckets(HashTable table)
    {
        _currentHashBucket = null;
        _currentHashEntry = null;

        _matchedHashEntries.Clear();

        var models = new List<HashBucketModel>(table.BucketCount);

        foreach (var bucket in table.Buckets)
        {
            var model = new HashBucketModel { Index = bucket.Index };

            for (var index = 0; index < bucket.Count; index++)
            {
                model.Entries.Add(ToEntryModel(bucket.Index, index, bucket.Entries[index]));
            }

            models.Add(model);
        }

        _hashBucketModels = models;
        _syncedHashRowCount = table.RowCount;
    }

    private void UpdateHashHighlight(AccessStep? step)
    {
        if (step is AccessStep.HashProbe or AccessStep.JoinStart)
        {
            foreach (var matched in _matchedHashEntries)
            {
                matched.IsMatched = false;
            }

            _matchedHashEntries.Clear();
        }

        if (_currentHashBucket is not null)
        {
            _currentHashBucket.IsCurrent = false;

            _currentHashBucket = null;
        }

        if (_currentHashEntry is not null)
        {
            _currentHashEntry.IsCurrent = false;

            _currentHashEntry = null;
        }

        var (bucketIndex, entryIndex) = step switch
        {
            AccessStep.HashBuild build => (build.Bucket, build.Entry),
            AccessStep.HashProbe probe => (probe.Bucket, -1),
            AccessStep.HashCompare compare => (compare.Bucket, compare.Entry),
            _ => (-1, -1)
        };

        if (_hashBucketModels is not { } models || bucketIndex < 0 || bucketIndex >= models.Count)
        {
            return;
        }

        _currentHashBucket = models[bucketIndex];
        _currentHashBucket.IsCurrent = true;

        if (entryIndex < 0 || entryIndex >= _currentHashBucket.Entries.Count)
        {
            return;
        }

        _currentHashEntry = _currentHashBucket.Entries[entryIndex];
        _currentHashEntry.IsCurrent = true;

        if (step is AccessStep.HashCompare { IsMatch: true })
        {
            _currentHashEntry.IsMatched = true;

            _matchedHashEntries.Add(_currentHashEntry);
        }
    }

    /// <summary>
    /// Builds one grid row, naming its bucket only on the first entry of a chain
    /// </summary>
    private HashEntryModel ToEntryModel(int bucketIndex, int entryIndex, HashEntry entry)
    {
        var columns = _hashColumns ?? [];

        var record = ToRecordModel(entry.Record);

        var cells = new List<HashCellModel>(columns.Count);

        for (var index = 0; index < columns.Count; index++)
        {
            var value = index switch
            {
                0 => entryIndex == 0 ? $"0x{bucketIndex:X2}" : string.Empty,
                1 => $"0x{entry.Hash:X8}",
                _ => index - 2 < record.Fields.Count ? record.Fields[index - 2].Value : string.Empty
            };

            cells.Add(new HashCellModel { Value = value, Column = columns[index] });
        }

        return new HashEntryModel { Cells = cells };
    }

    private static IndexRecordModel ToRecordModel(JoinBufferRow row)
    {
        var model = ToRecordModel(row.Record);

        model.IsMatched = row.IsMatched;

        return model;
    }

    private IndexRecordModel? ToResultModel(AccessStep step)
    {
        if (Kind is TraceKind.MergeJoin or TraceKind.KeyLookup or TraceKind.RidLookup or TraceKind.HashMatch)
        {
            return step is AccessStep.JoinEmit emit ? ToJoinedModel(emit) : null;
        }

        if (step is not AccessStep.Row { EmittedRecord: { } emitted })
        {
            return null;
        }

        return ToRecordModel(emitted);
    }

    private void UpdateInnerStrategy()
    {
        if (InnerStrategy is null && StepService is IJoinStepService { Inner.Strategy: { } inner })
        {
            InnerStrategy = inner;
        }
    }

    private async Task StartAsync()
    {
        var predicateInfo = PlanNode?.PredicateInfo;

        var evaluationContext = QueryTime is { } queryTime ? new EvaluationContext(queryTime) : null;

        var hasUntranslated = predicateInfo?.HasUntranslatedPredicate == true;

        if (Kind == TraceKind.RidLookup)
        {
            if (ResolveJoin() is not { } ridJoin)
            {
                return;
            }

            var ridService = (NestedLoopsStepService)StepService;

            var ridOuterPredicate = ridJoin.Outer.PredicateInfo;

            var ridOuterUnit = Visuals[0].AllocationUnit;

            IReadOnlyList<SeekBounds> ridRanges = ridOuterPredicate is { HasSeekBounds: true }
                                                  ? ridOuterPredicate.SeekBounds
                                                  : [SeekBounds.All];

            var ridOuterInput = new RangeDefinition(ridOuterUnit.AllocationUnitId, ridOuterUnit.RootPage, ridRanges)
            {
                Residual = ridJoin.Outer.HasRedundantResidual() ? null : ridOuterPredicate?.Residual,
                Direction = SideScanDirection(ridJoin.Outer),
                RowGoal = ridOuterPredicate?.RowGoal,
                HasUntranslatedResidual = ridOuterPredicate?.HasUntranslatedPredicate == true
            };

            var heapInner = new RidLookupJoinInput(HeapService!, ridJoin.Inner.PredicateInfo?.Residual);

            await Task.Run(() => ridService.StartAsync(Database,
                                                       ridOuterInput,
                                                       heapInner,
                                                       CancellationToken.None,
                                                       evaluationContext,
                                                       ridJoin.JoinType));
        }
        else if (Kind == TraceKind.KeyLookup)
        {
            if (ResolveJoin() is not { } join)
            {
                return;
            }

            var service = (NestedLoopsStepService)StepService;

            var outerPredicate = join.Outer.PredicateInfo;

            var outerUnit = Visuals[0].AllocationUnit;

            var innerUnit = Visuals[1].AllocationUnit;

            IReadOnlyList<SeekBounds> outerRanges = outerPredicate is { HasSeekBounds: true }
                                                    ? outerPredicate.SeekBounds
                                                    : [SeekBounds.All];

            var outerInput = new RangeDefinition(outerUnit.AllocationUnitId, outerUnit.RootPage, outerRanges)
            {
                Residual = join.Outer.HasRedundantResidual() ? null : outerPredicate?.Residual,
                Direction = SideScanDirection(join.Outer),
                RowGoal = outerPredicate?.RowGoal,
                HasUntranslatedResidual = outerPredicate?.HasUntranslatedPredicate == true
            };

            var bindings = (join.Inner.PredicateInfo?.CorrelatedSeekColumns ?? [])
                           .Select(c => new CorrelationBinding(c.Column, c.OuterColumn))
                           .ToList();

            if (bindings.Count == 0)
            {
                return;
            }

            var innerInput = new SeekDefinition(innerUnit.AllocationUnitId, innerUnit.RootPage, bindings)
            {
                Residual = join.Inner.PredicateInfo?.Residual,
                RowGoal = join.JoinType is JoinType.LeftSemi or JoinType.LeftAntiSemi ? 1 : null
            };

            await Task.Run(() => service.StartAsync(Database,
                                                    outerInput,
                                                    innerInput,
                                                    CancellationToken.None,
                                                    evaluationContext,
                                                    join.JoinType));
        }
        else if (Kind == TraceKind.MergeJoin)
        {
            if (PlanNode is null
                || MergeJoinResolver.Resolve(PlanNode) is not { } merge
                || PlanNode.MergeInfo is not { } mergeInfo)
            {
                return;
            }

            var service = (MergeJoinStepService)StepService;

            var outerInput = MergeSideInput(merge.Outer, Visuals[0].AllocationUnit, mergeInfo.OuterKeys);

            var innerInput = MergeSideInput(merge.Inner, Visuals[1].AllocationUnit, mergeInfo.InnerKeys);

            await Task.Run(() => service.StartAsync(Database,
                                                    outerInput,
                                                    innerInput,
                                                    CancellationToken.None,
                                                    evaluationContext,
                                                    merge.JoinType));
        }
        else if (Kind == TraceKind.HashMatch)
        {
            if (PlanNode is null
                || HashJoinResolver.Resolve(PlanNode) is not { } hash
                || PlanNode.HashInfo is not { } hashInfo)
            {
                return;
            }

            var service = (HashMatchStepService)StepService;

            var buildInput = HashSideInput(hash.Build, Visuals[0].AllocationUnit, hashInfo.BuildKeys);

            var probeInput = HashSideInput(hash.Probe, Visuals[1].AllocationUnit, hashInfo.ProbeKeys);

            await Task.Run(() => service.StartAsync(Database,
                                                    buildInput,
                                                    probeInput,
                                                    CancellationToken.None,
                                                    evaluationContext,
                                                    hash.JoinType,
                                                    residual: hashInfo.Residual));

            if (Visuals.FirstOrDefault(v => v.IsHashTableVisible) is { } buildVisual)
            {
                buildVisual.HashBucketCount = service.Table.BucketCount;
            }
        }
        else if (Kind == TraceKind.Allocation)
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

        UpdateInnerStrategy();

        IsStepping = true;
    }

    private static HashSideDefinition HashSideInput(PlanNode side,
                                                    AllocationUnit unit,
                                                    List<ColumnReference> keys)
    {
        var predicateInfo = side.PredicateInfo;

        IReadOnlyList<SeekBounds> ranges = predicateInfo is { HasSeekBounds: true }
                                           ? predicateInfo.SeekBounds
                                           : [SeekBounds.All];

        return new HashSideDefinition(unit.AllocationUnitId,
                                      unit.RootPage,
                                      ranges,
                                      [.. keys.Select(k => k.Column.Trim('[', ']'))])
        {
            RowEstimate = side.EstimatedRows > 0 ? side.EstimatedRows : side.RowsOutput,
            Residual = side.HasRedundantResidual() ? null : predicateInfo?.Residual,
            Direction = SideScanDirection(side),
            HasUntranslatedResidual = predicateInfo?.HasUntranslatedPredicate == true
        };
    }

    private static MergeSideDefinition MergeSideInput(PlanNode side,
                                                     AllocationUnit unit,
                                                     List<ColumnReference> keys)
    {
        var predicateInfo = side.PredicateInfo;

        IReadOnlyList<SeekBounds> ranges = predicateInfo is { HasSeekBounds: true }
                                           ? predicateInfo.SeekBounds
                                           : [SeekBounds.All];

        return new MergeSideDefinition(unit.AllocationUnitId,
                                      unit.RootPage,
                                      ranges,
                                      [.. keys.Select(k => k.Column.Trim('[', ']'))])
        {
            Residual = side.HasRedundantResidual() ? null : predicateInfo?.Residual,
            Direction = SideScanDirection(side),
            HasUntranslatedResidual = predicateInfo?.HasUntranslatedPredicate == true
        };
    }

    /// <summary>
    /// Builds the joined output row from the columns the join operator states it returns
    /// </summary>
    /// <remarks>
    /// The operator's output list is what the join actually hands upwards, so it fixes both the columns and their order. Anything a side
    /// read only to do its own work, a bookmark most of all, is left out. A column the preserved side of an outer join has no row for
    /// reads as NULL, which also keeps the grid's columns steady from row to row.
    /// </remarks>
    private IndexRecordModel ToJoinedModel(AccessStep.JoinEmit emit)
    {
        var outputColumns = PlanNode?.OutputColumns ?? [];

        var fields = outputColumns.Count > 0
            ? [.. outputColumns.Select(c => ToField(emit, c))]
            : Combine(emit);

        return new IndexRecordModel
        {
            Slot = emit.OuterRecord?.Slot ?? emit.InnerRecord?.Slot ?? 0,
            RowIdentifier = null,
            Fields = fields
        };
    }

    private IndexRecordFieldModel ToField(AccessStep.JoinEmit emit, ColumnReference column)
    {
        var name = column.Column.Trim('[', ']');

        var table = column.Table.Trim('[', ']');

        var sides = ResolveSides();

        // Where both sides carry the column, the output list says which table it came from
        var preferInner = sides is { } resolved
                          && !string.IsNullOrEmpty(table)
                          && string.Equals(table, resolved.Inner.Table?.Trim('[', ']'), StringComparison.OrdinalIgnoreCase);

        var field = preferInner
            ? Find(emit.InnerRecord, name) ?? Find(emit.OuterRecord, name)
            : Find(emit.OuterRecord, name) ?? Find(emit.InnerRecord, name);

        return new IndexRecordFieldModel
        {
            Name = name,
            Value = field?.Value ?? "NULL",
            DataType = field?.ColumnStructure.DataType ?? SqlDbType.Variant
        };
    }

    private static List<IndexRecordFieldModel> Combine(AccessStep.JoinEmit emit)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var fields = new List<IndexRecordFieldModel>();

        foreach (var record in new[] { emit.OuterRecord, emit.InnerRecord })
        {
            foreach (var field in record?.Fields ?? [])
            {
                fields.Add(new IndexRecordFieldModel
                {
                    Name = names.Add(field.Name) ? field.Name : $"Inner.{field.Name}",
                    Value = field.Value,
                    DataType = field.ColumnStructure.DataType
                });
            }
        }

        return fields;
    }

    private static RecordField? Find(IRecord? record, string name)
        => record?.Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    private static IndexRecordModel ToRecordModel(IRecord record)
    {
        return TraceVisualViewModel.ToRecordModel(record);
    }

    private static void Append(AccessStep step, ObservableCollection<AccessStep> history)
    {
        if (step is AccessStep.MergeCompare { Comparison: not 0 } compare
            && history.Count > 1
            && history[0] is AccessStep.Row or AccessStep.RowRun)
        {
            if (history[1] is AccessStep.MergeCompare previous
                && Math.Sign(previous.Comparison) == Math.Sign(compare.Comparison)
                && StaticKeyMatches(compare.Comparison, previous.OuterKey, previous.InnerKey, compare))
            {
                history[1] = new AccessStep.MergeCompareRun(Math.Sign(compare.Comparison), 2)
                {
                    OuterFrom = previous.OuterKey,
                    OuterTo = compare.OuterKey,
                    InnerFrom = previous.InnerKey,
                    InnerTo = compare.InnerKey,
                    Action = compare.Action,
                    Source = compare.Source,
                    Counters = compare.Counters
                };

                return;
            }

            if (history[1] is AccessStep.MergeCompareRun run
                && run.Comparison == Math.Sign(compare.Comparison)
                && StaticKeyMatches(compare.Comparison, run.OuterTo, run.InnerTo, compare))
            {
                history[1] = run with
                {
                    Count = run.Count + 1,
                    OuterTo = compare.OuterKey,
                    InnerTo = compare.InnerKey,
                    Counters = compare.Counters
                };

                return;
            }
        }

        if (step is AccessStep.Probe probe)
        {
            if (history.Count > 0 && history[0] is AccessStep.ProbeRun probeRun && probeRun.Source == probe.Source)
            {
                history[0] = new AccessStep.ProbeRun([probe, .. probeRun.Probes])
                {
                    Source = probe.Source,
                    Counters = probe.Counters
                };

                return;
            }

            step = new AccessStep.ProbeRun([probe])
            {
                Source = probe.Source,
                Counters = probe.Counters
            };
        }

        if (step is AccessStep.Row row && history.Count > 0)
        {
            var latest = history[0];

            if (latest is AccessStep.Row previous
                && previous.Source == row.Source
                && previous.Outcome == row.Outcome
                && previous.IsReadAhead == row.IsReadAhead
                && Math.Abs(row.Slot - previous.Slot) == 1)
            {
                history[0] = new AccessStep.RowRun(previous.Slot, row.Slot, row.Outcome)
                {
                    Count = 2,
                    HasResidual = row.HasResidual,
                    HasRange = row.HasRange,
                    EmitCount = EmitOf(previous) + EmitOf(row),
                    Counters = row.Counters,
                    Source = row.Source
                };

                return;
            }

            if (latest is AccessStep.RowRun run
                && run.Source == row.Source
                && run.Outcome == row.Outcome
                && !row.IsReadAhead
                && Math.Abs(row.Slot - run.ToSlot) == 1)
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

    private static bool StaticKeyMatches(int comparison, AccessKey previousOuter, AccessKey previousInner, AccessStep.MergeCompare compare)
    {
        return comparison < 0 ? previousInner.Equals(compare.InnerKey) : previousOuter.Equals(compare.OuterKey);
    }
}
