using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Descriptions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Iterators.Stepping;
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

public sealed partial class TraceTabViewModel : ObservableObject
{
    private const int HistoryLimit = 1000;

    private const double FastRunThresholdMs = 1000;

    private const double RunToEndDelayMs = -100;

    [ObservableProperty]
    private double _runDelayMs = 150;

    partial void OnRunDelayMsChanged(double value) => UpdateBlobDimming();

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

        Applier = new TraceStepApplier(layout, new TraceRowBuilder(definitions, sides), VisualsByNode, OperatorsByNode);

        foreach (var op in Operators)
        {
            op.ActivationRequested += ActivateOperator;

            op.PageOpenRequested += address => PageOpenRequested?.Invoke(this, address);

            Applier.BuildStateItems(op);
        }

        SelectedVisual = visuals[0];

        SelectedNodeId = Operators.Count > 0 ? Operators[0].NodeId : definition.NodeId;

        PlanNode = planNode;

        StepNodes = BuildStepNodes();

        Dock = BuildDock();
    }

    public IReadOnlyList<TraceVisualViewModel> Visuals { get; }

    /// <summary>
    /// The tab whose walk the strategy and description panels describe
    /// </summary>
    [ObservableProperty]
    private TraceVisualViewModel _selectedVisual;

    private IteratorDefinition Definition { get; }

    private IIteratorFactory IteratorFactory { get; }

    private IteratorStepper? Stepper { get; set; }

    private TraceLayout Layout { get; }

    private TraceStepApplier Applier { get; }

    private Dictionary<int, TraceVisualViewModel> VisualsByNode { get; }

    public IReadOnlyList<TraceOperatorViewModel> Operators { get; }

    private Dictionary<int, TraceOperatorViewModel> OperatorsByNode { get; }

    public DatabaseSource Database { get; }

    public AllocationUnit AllocationUnit => SelectedVisual.AllocationUnit;

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

    private Dictionary<int, PlanNode> _planNodesById = [];

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

    /// <summary>
    /// The operator whose tab was last clicked, which is the one the description describes
    /// </summary>
    [ObservableProperty]
    private int _selectedNodeId;

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

    private void NotifyDescriptionChanged()
    {
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
    }

    private DateTime? QueryTime { get; set; }

    [ObservableProperty]
    private ScanModeResult? _scanMode;

    public event EventHandler<PageNavigatedEventArgs>? PageNavigated;

    /// <summary>
    /// Raised when the page an operator stands on is clicked, which opens it as a tab of the query it was traced from
    /// </summary>
    public event EventHandler<PageAddress>? PageOpenRequested;

    private bool _hasNavigatedSinceReset;

    [ObservableProperty]
    private ObservableCollection<AccessStep> _stepHistory = [];

    [ObservableProperty]
    private AccessStep? _currentStep;

    /// <summary>
    /// What the join does with a pair that matches on both sides, or null when the trace is not a join
    /// </summary>
    public JoinDecision? JoinRule => Definition is JoinDefinition join ? join.JoinType.Decide(true, true) : null;

    public IteratorDefinition? SelectedDefinition => Layout.Nodes.GetValueOrDefault(SelectedNodeId)?.Definition;

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

    public Uri? SelectedOperatorIcon => SelectedHeader?.Icon;

    public string SelectedOperatorName => SelectedHeader?.Heading ?? string.Empty;

    /// <summary>
    /// The access path the selected operator settled on, or the one its definition already describes before the walk starts
    /// </summary>
    public AccessStrategy? SelectedStrategy => Applier.StrategyFor(SelectedNodeId) ?? PlannedStrategyFor(SelectedNodeId);

    /// <summary>
    /// Describes the selected operator, holding on to what was built until the strategy it describes is replaced
    /// </summary>
    /// <remarks>
    /// The description panel rebuilds itself whenever this changes, which is every step of a run if a fresh description is handed back
    /// each time it is read. Nothing about it changes between strategies, so the one built for a strategy is kept and returned again.
    /// </remarks>
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

    private readonly Dictionary<int, (AccessStrategy? Strategy, OperatorDescription Description)> _descriptions = [];

    private readonly Dictionary<int, AccessStrategy?> _plannedStrategies = [];

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

    public bool? SelectedIsOrdered => SelectedDefinition is RangeDefinition or SeekDefinition
        ? SelectedPlanNode?.ScanInfo?.IsOutputOrdered
        : null;

    /// <summary>
    /// True while the selected input is a correlated seek that has not yet been bound, so it has no descent to describe
    /// </summary>
    public bool IsSelectedStrategyPending
        => SelectedStrategy is null && IsStepping && SelectedDefinition is SeekDefinition or HeapFetchDefinition;

    [ObservableProperty]
    private IReadOnlyDictionary<int, TraceStepNode>? _stepNodes;

    private Dictionary<int, TraceStepNode> BuildStepNodes()
    {
        var names = new Dictionary<int, string>();

        foreach (var (nodeId, node) in Layout.Nodes)
        {
            var name = _planNodesById.GetValueOrDefault(nodeId)?.PhysicalOperator
                       ?? TraceLayoutBuilder.DisplayName(node.Definition);

            names[nodeId] = nodeId < 0 ? name : $"{name} (Node {nodeId})";
        }

        var nodes = new Dictionary<int, TraceStepNode>();

        foreach (var (nodeId, node) in Layout.Nodes)
        {
            nodes[nodeId] = new TraceStepNode(names[nodeId],
                                              node.Depth,
                                              node.Colour.ToWindowsColor(),
                                              node.InputNodes.Outer,
                                              node.InputNodes.Inner,
                                              TraceStepDescriber.NodeSummary(node.Definition, node.InputNodes, names),
                                              TraceStepDescriber.NodeSubtitle(node.Visual?.AllocationUnit));
        }

        return nodes;
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

    private CancellationTokenSource? _runToEndCancellation;

    public AccessCounters CurrentCounters => CurrentStep?.Counters ?? default;

    public bool IsWalkInProgress => IsStepping && !IsStepComplete;

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

    public bool IsStepDetailVisible => IsStepping && !IsRunning && !IsRunningToEnd;

    public TraceBlobPalette BlobPalette => Layout.Palette;

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

    private readonly Dictionary<int, int> _parentByNode = [];

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
        OnPropertyChanged(nameof(IsStepDetailVisible));
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
        OnPropertyChanged(nameof(IsStepDetailVisible));

        UpdateBlobDimming();
    }

    partial void OnIsRunningToEndChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStepDetailVisible));

        UpdateBlobDimming();
    }

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
            if (RunDelayMs < 0)
            {
                IsRunning = false;

                await RunToEnd();

                return;
            }

            await StepNext();

            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, RunDelayMs)));
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

        if (step is AccessStep.Stopped && step.NodeId == Definition.NodeId)
        {
            IsStepComplete = true;
            IsRunning = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task RunToEnd() => RunUntilAsync(null);

    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task RunTo(string target) => RunUntilAsync(StopCondition(target));

    /// <summary>
    /// Runs on to the next step the selected operator takes in a phase, which is what a phase's run button asks for
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task RunToPhase(AccessPhase phase) => RunUntilAsync(PhaseStopCondition(phase, SelectedDefinition, SelectedNodeId));

    private static Func<AccessStep, bool> PhaseStopCondition(AccessPhase phase, IteratorDefinition? definition, int nodeId)
    {
        return step => definition is not null
                       && OperatorPhases.Resolve(definition, step, step.NodeId == nodeId) == phase;
    }

    public const string EmitTarget = "Emit";

    public const string RebindTarget = "Rebind";

    public const string PhaseTarget = "Phase";

    public const string PageReadTarget = "PageRead";

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

    private sealed record RunResult(ObservableCollection<AccessStep> Steps,
                                    TraceStreamUpdate StreamUpdate,
                                    Dictionary<TraceVisualViewModel, TraceVisualReplay> Replays);

    private async Task RunUntilAsync(Func<AccessStep, bool>? stopAfter)
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

        return new RunResult(steps, Applier.ComputeStreamUpdate(stepper.History), replays);
    }

    private void ApplyRunResult(IteratorStepper stepper, RunResult result)
    {
        StepHistory = result.Steps;

        Applier.ApplyStreamUpdate(result.StreamUpdate);

        Applier.UpdateStrategies(stepper);

        Applier.UpdateOperatorStates(stepper);

        Applier.SyncPositions(stepper.History);

        foreach (var visual in Visuals)
        {
            visual.ApplyReplay(result.Replays[visual]);
        }

        Applier.AttachHashTables(stepper);

        Applier.SyncHeldRows(stepper);

        Applier.SyncHashTables(stepper.Current);

        NotifyDescriptionChanged();

        CurrentStep = stepper.Current;
        IsStepComplete = stepper.IsComplete;
    }

    [RelayCommand]
    public void ResetStep()
    {
        _runToEndCancellation?.Cancel();

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
}
