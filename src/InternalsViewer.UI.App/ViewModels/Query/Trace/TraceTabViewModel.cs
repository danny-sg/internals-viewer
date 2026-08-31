using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Descriptions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Iterators.RowMode.Stepping;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.Services.Query.Trace;
using InternalsViewer.UI.App.Services.Query.Trace.Steps;
using InternalsViewer.UI.App.ViewModels.Index;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceTabViewModel : ObservableObject, IDisposable
{
    public const string EmitTarget = "Emit";

    public const string RebindTarget = "Rebind";

    public const string PhaseTarget = "Phase";

    public const string PageReadTarget = "PageRead";
    private const int HistoryLimit = 1000;

    private const double FastRunThresholdMs = 1000;

    private const double RunToEndDelayMs = -100;

    private readonly Dictionary<int, (AccessStrategy? Strategy, OperatorDescription Description)> _descriptions = [];

    private readonly Dictionary<int, AccessStrategy?> _plannedStrategies = [];

    private readonly Dictionary<int, int> _parentByNode = [];

    private Dictionary<int, PlanNode> _planNodesById = [];

    private bool _isDescriptionStale;

    private bool _hasNavigatedSinceReset;

    private CancellationTokenSource? _runToEndCancellation;

    private CancellationTokenSource? _interactiveCancellation;

    private Task? _interactiveRun;


    [ObservableProperty]
    private double _runDelayMs = 150;

    /// <summary>
    /// The tab whose walk the strategy and description panels describe
    /// </summary>
    [ObservableProperty]
    private TraceVisualViewModel _selectedVisual;

    [ObservableProperty]
    private PlanNode? _planNode;

    /// <summary>
    /// The traced operator and everything below it, which is the part of the plan this trace runs
    /// </summary>
    /// <remarks>
    /// A trace is offered only where every operator below the one traced can be simulated, so the subtree is the traced set already and
    /// no further filtering is needed to keep the untraced remainder of the query out.
    /// </remarks>
    [ObservableProperty]
    private ExecutionPlan? _tracePlan;

    /// <summary>
    /// The operator whose tab was last clicked, which is the one the description describes
    /// </summary>
    [ObservableProperty]
    private int _selectedNodeId;

    [ObservableProperty]
    private ScanModeResult? _scanMode;

    [ObservableProperty]
    private ObservableCollection<AccessStep> _stepHistory = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorName))]
    [NotifyPropertyChangedFor(nameof(IsActiveOperatorVisible))]
    private AccessStep? _currentStep;

    [ObservableProperty]
    private IReadOnlyDictionary<int, TraceStepNode>? _stepNodes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorName))]
    [NotifyPropertyChangedFor(nameof(IsActiveOperatorVisible))]
    private bool _isStepping;

    [ObservableProperty]
    private bool _isStepComplete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorName))]
    [NotifyPropertyChangedFor(nameof(IsActiveOperatorVisible))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveOperatorName))]
    [NotifyPropertyChangedFor(nameof(IsActiveOperatorVisible))]
    private bool _isRunningToEnd;

    [ObservableProperty]
    private long _playheadTimeUs;

    public TraceTabViewModel(IIteratorFactory iteratorFactory,
                             IteratorDefinition definition,
                             DatabaseSource database,
                             PlanNode? planNode,
                             DateTime? queryTime,
                             ScanModeResult? scanMode,
                             IReadOnlyList<TraceVisualViewModel> visuals,
                             TraceLayout layout)
    {
        IteratorFactory = iteratorFactory;
        Definition = definition;
        Database = database;
        QueryTime = queryTime;
        ScanMode = scanMode;
        Visuals = visuals;
        Layout = layout;

        VisualsByNode = visuals.ToDictionary(v => v.NodeId);

        foreach (var visual in visuals)
        {
            if (layout.Nodes.TryGetValue(visual.NodeId, out var node))
            {
                visual.OperatorColour = node.Colour;
            }
        }

        Operators = layout.Tabs;

        OperatorsByNode = Operators.ToDictionary(o => o.NodeId);

        IndexParents(definition);

        var definitions = layout.Nodes.ToDictionary(n => n.Key, n => n.Value.Definition);

        var sides = layout.Nodes.Where(n => n.Value.Sides is not null)
                                .ToDictionary(n => n.Key, n => n.Value.Sides!);

        Batches = [.. BatchOwners.Find(definition)
                                 .Select(d => new TraceBatchViewModel
                                 {
                                     NodeId = d.NodeId,
                                     Title = OperatorsByNode.GetValueOrDefault(d.NodeId)?.Title ?? "Batch"
                                 })];

        BatchesByNode = Batches.ToDictionary(b => b.NodeId);

        Applier = new TraceStepApplier(layout,
                                       new TraceRowBuilder(definitions, sides),
                                       VisualsByNode,
                                       OperatorsByNode,
                                       BatchesByNode);

        Applier.BatchTouched += OnBatchTouched;

        foreach (var op in Operators)
        {
            op.ActivationRequested += ActivateOperator;

            op.PageOpenRequested += address => PageOpenRequested?.Invoke(this, address);

            op.ZoomToPageRequested += value => IsZoomToPage = value;

            Applier.BuildStateItems(op);
        }

        SelectedVisual = visuals[0];

        SelectedNodeId = Operators.Count > 0 ? Operators[0].NodeId : definition.NodeId;

        PlanNode = planNode;

        StepNodes = BuildStepNodes();

        Dock = BuildDock();
    }

    public event EventHandler<PageNavigatedEventArgs>? PageNavigated;

    /// <summary>
    /// Raised when the page an operator stands on is clicked, which opens it as a tab of the query it was traced from
    /// </summary>
    public event EventHandler<PageAddress>? PageOpenRequested;

    public IReadOnlyList<TraceVisualViewModel> Visuals { get; }

    public IReadOnlyList<TraceOperatorViewModel> Operators { get; }

    public DatabaseSource Database { get; }

    public AllocationUnit AllocationUnit => SelectedVisual.AllocationUnit;

    /// <summary>
    /// What the join does with a pair that matches on both sides, or null when the trace is not a join
    /// </summary>
    public JoinDecision? JoinRule => Definition is JoinDefinition join ? join.JoinType.Decide(true, true) : null;

    /// <summary>
    /// The batches of the plan, one per operator that creates one
    /// </summary>
    public IReadOnlyList<TraceBatchViewModel> Batches { get; }

    private IReadOnlyDictionary<int, TraceBatchViewModel> BatchesByNode { get; }

    private void OnBatchTouched(int nodeId)
    {
        if (!IsRunning && !IsRunningToEnd)
        {
            ActiveBatchNodeId = nodeId;
        }
    }

    /// <summary>
    /// The batch the playhead last touched, which is the tab to bring forward
    /// </summary>
    [ObservableProperty]
    private int _activeBatchNodeId = int.MinValue;

    /// <summary>
    /// The operator the playhead is on, which only stands still while stepping
    /// </summary>
    public string ActiveOperatorName
        => CurrentStep is { } step && OperatorsByNode.TryGetValue(step.NodeId, out var op) ? op.Heading : string.Empty;

    public bool IsActiveOperatorVisible
        => IsStepping && !IsRunning && !IsRunningToEnd && ActiveOperatorName.Length > 0;

    public IteratorDefinition? SelectedDefinition => Layout.Nodes.GetValueOrDefault(SelectedNodeId)?.Definition;

    public Uri? SelectedOperatorIcon => SelectedHeader?.Icon;

    public string SelectedOperatorName => SelectedHeader?.Heading ?? string.Empty;

    public AccessStrategy? SelectedStrategy => Applier.StrategyFor(SelectedNodeId) ?? PlannedStrategyFor(SelectedNodeId);

    public OperatorDescription? SelectedDescription
    {
        get
        {
            if (SelectedDefinition is not { } definition)
            {
                return null;
            }

            var strategy = SelectedStrategy;

            if (_descriptions.TryGetValue(SelectedNodeId, out var cached) && ReferenceEquals(cached.Strategy, strategy))
            {
                return cached.Description;
            }

            var description = OperatorDescriptionBuilder.Build(definition, strategy);

            _descriptions[SelectedNodeId] = (strategy, description);

            return description;
        }
    }

    public AccessPhase? SelectedPhase
    {
        get
        {
            if (CurrentStep is not { } step || SelectedDefinition is not { } definition)
            {
                return null;
            }

            return OperatorPhases.Resolve(definition, step, step.NodeId == SelectedNodeId);
        }
    }

    public PlanNode? SelectedPlanNode => _planNodesById.GetValueOrDefault(SelectedNodeId);

    public string? SelectedPhysicalOperator => SelectedDefinition is SelectDefinition ? null : SelectedPlanNode?.PhysicalOperator;

    public string? SelectedLogicalOperator => SelectedDefinition is SelectDefinition ? null : SelectedPlanNode?.LogicalOperator;

    /// <summary>
    /// What the plan recorded for the selected operator's memory grant, which the trace shows beside the memory it modelled
    /// </summary>
    public PlanMemoryGrant? SelectedMemoryGrant => SelectedPlanNode?.MemoryGrant;

    public bool? SelectedIsOrdered => SelectedDefinition is RangeDefinition or SeekDefinition
        ? SelectedPlanNode?.ScanInfo?.IsOutputOrdered
        : null;

    /// <summary>
    /// True while the selected input is a correlated seek that has not yet been bound, so it has no descent to describe
    /// </summary>
    public bool IsSelectedStrategyPending
        => SelectedStrategy is null && IsStepping && SelectedDefinition is SeekDefinition or HeapFetchDefinition;

    public AccessCounters CurrentCounters => CurrentStep?.Counters ?? default;

    public bool IsWalkInProgress => IsStepping && !IsStepComplete;

    public TraceBlobPalette BlobPalette => Layout.Palette;

    private IteratorDefinition Definition { get; }

    private IIteratorFactory IteratorFactory { get; }

    private IteratorStepper? Stepper { get; set; }

    private TraceLayout Layout { get; }

    private TraceStepApplier Applier { get; }

    private Dictionary<int, TraceVisualViewModel> VisualsByNode { get; }

    private Dictionary<int, TraceOperatorViewModel> OperatorsByNode { get; }

    private DateTime? QueryTime { get; set; }

    /// <summary>
    /// Names the selected operator, which is built here rather than taken from its tab because a join's inputs have no tab of their own
    /// </summary>
    private OperatorHeader? SelectedHeader
    {
        get
        {
            if (SelectedDefinition is not { } definition)
            {
                return null;
            }

            var node = SelectedPlanNode ?? (definition is SelectDefinition
                ? new PlanNode { PhysicalOperator = "SELECT", IsStatement = true }
                : null);

            return OperatorHeader.For(definition, node);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task Run()
    {
        if (IsRunning)
        {
            IsRunning = false;

            return;
        }

        _interactiveRun = RunInteractiveAsync();

        await _interactiveRun;
    }

    [RelayCommand]
    public async Task StepNext()
    {
        if (HasBatchAdapter)
        {
            await StepToBatchAsync();

            return;
        }

        if (!IsStepping)
        {
            await StartAsync();
        }

        if (Stepper is not { } stepper)
        {
            return;
        }

        var step = await Task.Run(() => stepper.StepNextAsync(CancellationToken.None));

        if (step is null)
        {
            IsStepComplete = true;
            IsRunning = false;

            return;
        }

        TraceStepRuns.Append(step, StepHistory, HistoryLimit);

        Applier.ApplyStep(stepper, step);

        NotifyDescriptionChanged();

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
                Selection = SelectedVisual.VisualType == TraceVisualType.Allocation ? PageReadSelection.Last : PageReadSelection.Next
            });

            _hasNavigatedSinceReset = true;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task RunToEnd() => RunUntilAsync(null);

    public bool HasBatchAdapter => Layout.Nodes.Values.Any(n => n.Definition is BatchToRowDefinition);

    private static bool IsBatchBoundary(AccessStep step)
        => step is AccessStep.BatchProduced
                   or AccessStep.FilterVector
                   or AccessStep.BatchFiltered
                   or AccessStep.ComputeVector
                   or AccessStep.RowGroupOpened
                   or AccessStep.RowGroupSkipped
                   or AccessStep.SegmentElimination
                   or AccessStep.Stopped
                   or AccessStep.Close;

    private async Task StepToBatchAsync()
    {
        if (!IsStepping)
        {
            await StartAsync();
        }

        if (Stepper is not { } stepper)
        {
            return;
        }

        AccessStep? last = null;

        while (true)
        {
            var step = await Task.Run(() => stepper.StepNextAsync(CancellationToken.None));

            if (step is null)
            {
                IsStepComplete = true;

                IsRunning = false;

                break;
            }

            TraceStepRuns.Append(step, StepHistory, HistoryLimit);

            Applier.ApplyStep(stepper, step);

            last = step;

            if (IsBatchBoundary(step) || IsBreakpointHit(step))
            {
                break;
            }
        }

        if (last is null)
        {
            return;
        }

        NotifyDescriptionChanged();

        CurrentStep = last;
    }

    public bool HasBreakpoints => Operators.Any(o => o.IsBreakpoint);

    private bool IsBreakpointHit(AccessStep step)
        => OperatorsByNode.GetValueOrDefault(step.NodeId)?.IsBreakpoint == true;

    private Func<AccessStep, bool>? WithBreakpoints(Func<AccessStep, bool>? stopAfter)
    {
        if (!HasBreakpoints)
        {
            return stopAfter;
        }

        return stopAfter is null
            ? IsBreakpointHit
            : step => stopAfter(step) || IsBreakpointHit(step);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task RunTo(string target) => RunUntilAsync(StopCondition(target));

    /// <summary>
    /// Runs on to the next step the selected operator takes in a phase, which is what a phase's run button asks for
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task RunToPhase(AccessPhase phase) => RunUntilAsync(PhaseStopCondition(phase, SelectedDefinition, SelectedNodeId));

    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task RunToNextBatch()
        => RunUntilAsync(static step => step is AccessStep.BatchProduced);

    [RelayCommand]
    public void ResetStep()
    {
        _runToEndCancellation?.Cancel();
        _interactiveCancellation?.Cancel();

        if (Stepper is { } stepper)
        {
            Stepper = null;

            _ = stepper.DisposeAsync();
        }

        IsRunning = false;
        IsRunningToEnd = false;
        IsStepping = false;
        IsStepComplete = false;

        _hasNavigatedSinceReset = false;

        StepHistory = [];

        Applier.Reset();

        CurrentStep = null;

        foreach (var visual in Visuals)
        {
            visual.Reset();
        }

        NotifyDescriptionChanged();
    }

    public void Dispose()
    {
        _runToEndCancellation?.Cancel();
        _interactiveCancellation?.Cancel();

        if (Stepper is { } stepper)
        {
            Stepper = null;

            _ = stepper.DisposeAsync();
        }
    }

    partial void OnRunDelayMsChanged(double value) => UpdateBlobDimming();

    partial void OnPlanNodeChanged(PlanNode? value)
    {
        TracePlan = value is null ? null : new ExecutionPlan(0) { Root = [value] };

        _planNodesById = [];

        if (value is not null)
        {
            IndexPlanNodes(value);
        }

        StepNodes = BuildStepNodes();

        UpdateActivePlanNodes();

        OnPropertyChanged(nameof(SelectedPlanNode));

        NotifyDescriptionChanged();
    }

    private void IndexPlanNodes(PlanNode node)
    {
        _planNodesById[node.NodeId] = node;

        foreach (var child in node.Children)
        {
            IndexPlanNodes(child);
        }
    }

    partial void OnSelectedVisualChanged(TraceVisualViewModel value)
    {
        OnPropertyChanged(nameof(AllocationUnit));
    }

    partial void OnSelectedNodeIdChanged(int value)
    {
        MarkSelectedDocument();

        UpdateActivePlanNodes();

        if (Layout.Nodes.GetValueOrDefault(value) is { } node && (node.Visual ?? node.SourceVisual) is { } visual)
        {
            SelectedVisual = visual;
        }

        NotifyDescriptionChanged();
    }

    /// <summary>
    /// Tells the description panel what the selected operator now is, holding it back while a run is in flight
    /// </summary>
    /// <remarks>
    /// The panel is built from these, and a run passes through a phase per step. Nothing is read off the panel while the steps are going
    /// by, so the notification is held to the end of the run and raised once, rather than rebuilding the panel for every step taken.
    /// </remarks>
    private void NotifyDescriptionChanged()
    {
        if (IsRunning || IsRunningToEnd)
        {
            _isDescriptionStale = true;

            return;
        }

        _isDescriptionStale = false;

        OnPropertyChanged(nameof(SelectedDefinition));
        OnPropertyChanged(nameof(SelectedOperatorIcon));
        OnPropertyChanged(nameof(SelectedOperatorName));
        OnPropertyChanged(nameof(SelectedPlanNode));
        OnPropertyChanged(nameof(SelectedStrategy));
        OnPropertyChanged(nameof(SelectedDescription));
        OnPropertyChanged(nameof(SelectedPhase));
        OnPropertyChanged(nameof(IsSelectedStrategyPending));
        OnPropertyChanged(nameof(SelectedPhysicalOperator));
        OnPropertyChanged(nameof(SelectedLogicalOperator));
        OnPropertyChanged(nameof(SelectedIsOrdered));
        OnPropertyChanged(nameof(SelectedMemoryGrant));
    }

    private void FlushDescriptionIfStale()
    {
        if (_isDescriptionStale && !IsRunning && !IsRunningToEnd)
        {
            NotifyDescriptionChanged();
        }
    }

    private AccessStrategy? PlannedStrategyFor(int nodeId)
    {
        if (_plannedStrategies.TryGetValue(nodeId, out var cached))
        {
            return cached;
        }

        var strategy = PlannedStrategy(Layout.Nodes.GetValueOrDefault(nodeId)?.Definition);

        _plannedStrategies[nodeId] = strategy;

        return strategy;
    }

    private Dictionary<int, TraceStepNode> BuildStepNodes()
    {
        var names = new Dictionary<int, string>();

        foreach (var (nodeId, node) in Layout.Nodes)
        {
            var name = _planNodesById.GetValueOrDefault(nodeId)?.PhysicalOperator
                       ?? TraceLayoutBuilder.DisplayName(node.Definition);

            names[nodeId] = nodeId < 0 ? name : $"{name} ({nodeId})";
        }

        var nodes = new Dictionary<int, TraceStepNode>();

        foreach (var (nodeId, node) in Layout.Nodes)
        {
            nodes[nodeId] = new TraceStepNode(names[nodeId],
                                              node.Depth,
                                              node.Colour.ToWindowsColor(),
                                              node.InputNodes.Outer,
                                              node.InputNodes.Inner);
        }

        return nodes;
    }

    partial void OnPlayheadTimeUsChanged(long value)
    {
        foreach (var visual in Visuals)
        {
            visual.PlayheadTimeUs = value;
        }
    }

    partial void OnIsZoomToPageChanged(bool value)
    {
        foreach (var visual in Visuals)
        {
            visual.IsZoomToPage = value;
        }

        foreach (var op in Operators)
        {
            op.IsZoomToPage = value;
        }
    }

    /// <summary>
    /// The strategy the selected operator will use, worked out before the walk starts
    /// </summary>
    /// <remarks>
    /// Once opened the iterator publishes what it actually settled on, which supersedes this. Until then the definition already says
    /// enough to describe the descent, which is what makes the panel useful before anything has been stepped.
    /// </remarks>
    private AccessStrategy? PlannedStrategy(IteratorDefinition? definition)
    {
        if (definition is AllocationScanDefinition allocation)
        {
            return AccessStrategyBuilder.BuildAllocationScan(allocation.Residual, allocation.RowGoal) with
            {
                EntryPoint = allocation.FirstIamPage,
                EntryPointSource = "sys.sysallocunits.pgfirstiam"
            };
        }

        if (definition is not RangeDefinition range)
        {
            return null;
        }

        var structure = IndexStructureProvider.GetIndexStructure(Database, range.AllocationUnitId);

        return AccessStrategyBuilder.Build(structure,
                                           range.Ranges.Count > 0 ? range.Ranges[0] : SeekBounds.All,
                                           range.Direction,
                                           range.RowGoal,
                                           range.Residual,
                                           ranges: range.Ranges) with
        {
            EntryPoint = range.RootPage,
            EntryPointSource = "sys.sysallocunits.pgroot"
        };
    }

    partial void OnCurrentStepChanged(AccessStep? value)
    {
        OnPropertyChanged(nameof(SelectedPhase));
        OnPropertyChanged(nameof(CurrentCounters));

        UpdateBlobDimming();
    }

    private void UpdateBlobDimming()
    {
        if (!IsStepping || IsRunningToEnd || IsStepComplete || CurrentStep is not { } step)
        {
            Layout.Palette.SetActiveSet(null);

            return;
        }

        if (IsRunning && RunDelayMs < FastRunThresholdMs)
        {
            Layout.Palette.SetActiveSet(PathTo(step.NodeId));
        }
        else
        {
            Layout.Palette.SetActive(step.NodeId);
        }
    }

    private void IndexParents(IteratorDefinition definition)
    {
        foreach (var child in DefinitionTreeWalker.ChildrenOf(definition))
        {
            _parentByNode[child.NodeId] = definition.NodeId;

            IndexParents(child);
        }
    }

    private List<int> PathTo(int nodeId)
    {
        var path = new List<int> { nodeId };

        while (_parentByNode.TryGetValue(nodeId, out var parent))
        {
            path.Add(parent);

            nodeId = parent;
        }

        return path;
    }

    partial void OnIsSteppingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsWalkInProgress));
        OnPropertyChanged(nameof(IsSelectedStrategyPending));

        UpdateBlobDimming();
    }

    partial void OnIsStepCompleteChanged(bool value)
    {
        OnPropertyChanged(nameof(IsWalkInProgress));

        UpdateBlobDimming();
    }

    partial void OnIsRunningChanged(bool value)
    {
        UpdateBlobDimming();

        FlushDescriptionIfStale();
    }

    partial void OnIsRunningToEndChanged(bool value)
    {
        UpdateBlobDimming();

        FlushDescriptionIfStale();
    }

    private async Task RunInteractiveAsync()
    {
        IsRunning = true;

        _interactiveCancellation = new CancellationTokenSource();

        var cancellationToken = _interactiveCancellation.Token;

        try
        {
            while (IsRunning && !IsStepComplete)
            {
                if (RunDelayMs <= -500)
                {
                    IsRunning = false;

                    await RunToEnd();

                    return;
                }

                await StepNext();

                if (CurrentStep is { } current && IsBreakpointHit(current))
                {
                    IsRunning = false;

                    return;
                }

                var delay = RunDelayMs < 0 ? 1 : Math.Max(1, RunDelayMs);

                await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsRunning = false;

            _interactiveCancellation?.Dispose();
            _interactiveCancellation = null;
        }
    }

    private async Task StopInteractiveRunAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;

        if (_interactiveCancellation is { } interactiveCancellation)
        {
            await interactiveCancellation.CancelAsync();
        }

        if (_interactiveRun is { } interactive)
        {
            await interactive;
        }
    }

    private static Func<AccessStep, bool> PhaseStopCondition(AccessPhase phase, IteratorDefinition? definition, int nodeId)
    {
        return step => definition is not null
                       && OperatorPhases.Resolve(definition, step, step.NodeId == nodeId) == phase;
    }

    private Func<AccessStep, bool>? StopCondition(string target)
        => target switch
        {
            EmitTarget => step => step.NodeId == Definition.NodeId
                                  && step is AccessStep.JoinEmit
                                          or AccessStep.TopRow { EmittedRecord: not null }
                                          or AccessStep.Output { EmittedRecord: not null }
                                          or AccessStep.ConcatRow { EmittedRecord: not null }
                                          or AccessStep.SortRow { EmittedRecord: not null }
                                          or AccessStep.Row { EmittedRecord: not null },
            RebindTarget => step => step is AccessStep.Rebind,
            PhaseTarget => step => step is AccessStep.JoinStart or AccessStep.Reseek,
            PageReadTarget => step => step is AccessStep.ReadPage,
            _ => null
        };

    private async Task RunUntilAsync(Func<AccessStep, bool>? stopAfter)
    {
        stopAfter = WithBreakpoints(stopAfter);

        if (IsRunningToEnd)
        {
            if (_runToEndCancellation is { } runToEndCancellation)
            {
                await runToEndCancellation.CancelAsync();
            }

            return;
        }

        await StopInteractiveRunAsync();

        IsRunningToEnd = true;

        _runToEndCancellation = new CancellationTokenSource();

        var cancellationToken = _runToEndCancellation.Token;

        try
        {
            if (!IsStepping)
            {
                await StartAsync();
            }

            if (Stepper is not { } stepper)
            {
                return;
            }

            var result = await Task.Run(() => RunLoopAsync(stepper, stopAfter, cancellationToken), CancellationToken.None);

            ApplyRunResult(stepper, result);
        }
        finally
        {
            IsRunningToEnd = false;

            _runToEndCancellation.Dispose();
            _runToEndCancellation = null;
        }
    }

    private async Task<RunResult> RunLoopAsync(IteratorStepper stepper, Func<AccessStep, bool>? stopAfter, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested
                   && await stepper.StepNextAsync(CancellationToken.None) is { } step)
            {
                if (stopAfter?.Invoke(step) == true)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        var steps = new ObservableCollection<AccessStep>();

        foreach (var step in stepper.History)
        {
            TraceStepRuns.Append(step, steps, HistoryLimit);
        }

        var replays = Visuals.ToDictionary(v => v, v => v.ComputeReplay(stepper.History));

        return new RunResult(steps,
                             Applier.ComputeStreamUpdate(stepper.History),
                             Applier.ComputePositions(stepper.History),
                             Applier.ComputeHeldRows(stepper),
                             replays);
    }

    private void ApplyRunResult(IteratorStepper stepper, RunResult result)
    {
        StepHistory = result.Steps;

        Applier.ApplyStreamUpdate(result.StreamUpdate);

        Applier.UpdateStrategies(stepper);

        Applier.UpdateOperatorStates(stepper);

        Applier.ApplyPositionUpdate(result.PositionUpdate);

        foreach (var visual in Visuals)
        {
            visual.ApplyReplay(result.Replays[visual]);
        }

        Applier.AttachHashTables(stepper);

        Applier.ApplyHeldRows(result.HeldRows);

        Applier.SyncAggregates(stepper);

        Applier.SyncSegments(stepper);

        Applier.SyncHashTables(stepper.Current);

        Applier.SyncBatches(stepper);

        NotifyDescriptionChanged();

        CurrentStep = stepper.Current;
        IsStepComplete = stepper.IsComplete;
    }

    private async Task StartAsync()
    {
        var evaluationContext = QueryTime is { } queryTime ? new EvaluationContext(queryTime) : EvaluationContext.Now;

        var context = new IteratorContext(Database) { EvaluationContext = evaluationContext };

        var stepper = new IteratorStepper(IteratorFactory.Create(Definition), Definition, context);

        Stepper = stepper;

        await Task.Run(() => stepper.StartAsync(CancellationToken.None));

        Applier.UpdateStrategies(stepper);

        NotifyDescriptionChanged();

        Applier.AttachHashTables(stepper);

        IsStepping = true;
    }

    private sealed record RunResult(ObservableCollection<AccessStep> Steps,
                                    TraceStreamUpdate StreamUpdate,
                                    TracePositionUpdate PositionUpdate,
                                    Dictionary<(int NodeId, int InputIndex), HeldRowsSnapshot> HeldRows,
                                    Dictionary<TraceVisualViewModel, TraceVisualReplay> Replays);
}
