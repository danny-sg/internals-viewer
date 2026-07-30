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
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Allocations;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Internals.Services.Joins;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Views.Query.Tabs.Trace;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace InternalsViewer.UI.App.ViewModels.Query;

public enum TraceKind
{
    Index,
    Allocation,
    KeyLookup
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
                                     NestedLoopsStepService.OuterSource),
            new TraceVisualViewModel(TraceVisualKind.Index,
                                     database,
                                     innerUnit,
                                     indexService,
                                     $"Lookup: {innerUnit.IndexName}",
                                     NestedLoopsStepService.InnerSource)
        };

        return new TraceTabViewModel(TraceKind.KeyLookup, service, database, outerUnit, joinNode, queryTime, null, visuals);
    }
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
                             IReadOnlyList<TraceVisualViewModel> visuals)
    {
        Kind = kind;
        StepService = stepService;
        Database = database;
        AllocationUnit = allocationUnit;
        PlanNode = planNode;
        QueryTime = queryTime;
        ScanMode = scanMode;
        Visuals = visuals;

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

        var details = new TabGroupNode(description, results, strategy);

        var right = new SplitNode(Orientation.Vertical, new TabGroupNode(steps), details);

        return new DockLayoutViewModel(new SplitNode(Orientation.Horizontal, new TabGroupNode(visualDocuments), right));
    }

    public TraceKind Kind { get; }

    public IReadOnlyList<TraceVisualViewModel> Visuals { get; }

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

    public Brush? InnerAccentBrush
    {
        get
        {
            if (Visuals.Count < 2)
            {
                return null;
            }

            var colour = Visuals[1].ObjectColour;

            return new SolidColorBrush(Windows.UI.Color.FromArgb(colour.A, colour.R, colour.G, colour.B));
        }
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
            if (Kind == TraceKind.KeyLookup)
            {
                if (ResolveJoin() is not { } join)
                {
                    return null;
                }

                var outerPredicate = join.Outer.PredicateInfo;

                var outerUnit = Visuals[0].AllocationUnit;

                var outerStructure = IndexStructureProvider.GetIndexStructure(Database, outerUnit.AllocationUnitId);

                IReadOnlyList<SeekBounds> outerRanges = outerPredicate is { HasSeekBounds: true }
                                                        ? outerPredicate.SeekBounds
                                                        : [SeekBounds.All];

                return AccessStrategyBuilder.Build(outerStructure,
                                                   outerRanges[0],
                                                   OuterScanDirection(join),
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

    private static ScanDirection OuterScanDirection(CorrelatedJoin join)
        => join.Outer.ScanInfo?.IsForward == false ? ScanDirection.Backward : ScanDirection.Forward;

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

        if (step is AccessStep.Row { EmittedRecord: { } emitted } && IsResultRow(step))
        {
            ResultRecords.Add(ToRecordModel(emitted));

            UpdateResultsTitle();
        }

        UpdateInnerStrategy();

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
        foreach (var visual in Visuals)
        {
            visual.IsDimmed = Visuals.Count > 1
                              && step is not null
                              && !IsStepComplete
                              && visual.Source != step.Source;
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

                    if (step is AccessStep.Row { EmittedRecord: { } emitted } && IsResultRow(step))
                    {
                        results.Add(ToRecordModel(emitted));
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
        CurrentStep = null;
        Strategy = SeekDescription;
        InnerStrategy = null;

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

    private bool IsResultRow(AccessStep step)
    {
        return Kind != TraceKind.KeyLookup || step.Source == NestedLoopsStepService.InnerSource;
    }

    private void UpdateInnerStrategy()
    {
        if (InnerStrategy is null && StepService is NestedLoopsStepService { InnerStrategy: { } inner })
        {
            InnerStrategy = inner;
        }
    }

    private async Task StartAsync()
    {
        var predicateInfo = PlanNode?.PredicateInfo;

        var evaluationContext = QueryTime is { } queryTime ? new EvaluationContext(queryTime) : null;

        var hasUntranslated = predicateInfo?.HasUntranslatedPredicate == true;

        if (Kind == TraceKind.KeyLookup)
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

            var outerInput = new NestedLoopsOuterInput(outerUnit.AllocationUnitId, outerUnit.RootPage, outerRanges)
            {
                Residual = join.Outer.HasRedundantResidual() ? null : outerPredicate?.Residual,
                Direction = OuterScanDirection(join),
                RowGoal = outerPredicate?.RowGoal,
                HasUntranslatedResidual = outerPredicate?.HasUntranslatedPredicate == true
            };

            var bindings = join.Inner.PredicateInfo!.CorrelatedSeekColumns
                               .Select(c => new CorrelationBinding(c.Column, c.OuterColumn))
                               .ToList();

            var innerInput = new NestedLoopsInnerInput(innerUnit.AllocationUnitId, innerUnit.RootPage, bindings)
            {
                Residual = join.Inner.PredicateInfo?.Residual,
                RowGoal = 1
            };

            await Task.Run(() => service.StartAsync(Database, outerInput, innerInput, CancellationToken.None, evaluationContext));
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

            if (latest is AccessStep.Row previous
                && previous.Source == row.Source
                && previous.Outcome == row.Outcome
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
}
