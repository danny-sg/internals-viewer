using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Trace;
using InternalsViewer.UI.App.Services.Trace;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.Views.Query.Tabs.Trace;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed class TraceTabViewModelFactory(IIteratorFactory iteratorFactory, IndexService indexService)
{
    /// <summary>
    /// Builds a trace of an operator and everything below it, or null when some operator in that tree cannot be simulated
    /// </summary>
    public TraceTabViewModel? Create(DatabaseSource database,
                                     PlanNode node,
                                     Func<PlanNode, AllocationUnit?> resolveUnit,
                                     DateTime? queryTime,
                                     ScanModeResult? scanMode)
    {
        var builder = new TraceDefinitionBuilder(resolveUnit, database);

        if (builder.Build(node) is not { } definition)
        {
            return null;
        }

        var visuals = TraceSourceCollector.Collect(definition)
                                          .Select(s => CreateVisual(database, s, builder))
                                          .OfType<TraceVisualViewModel>()
                                          .ToList();

        if (visuals.Count == 0)
        {
            return null;
        }

        var layout = TraceLayoutBuilder.Build(definition,
                                              visuals.ToDictionary(v => v.NodeId),
                                              id => builder.Nodes.GetValueOrDefault(id));

        return new TraceTabViewModel(iteratorFactory, definition, database, node, queryTime, scanMode, visuals, layout);
    }

    private TraceVisualViewModel? CreateVisual(DatabaseSource database, TraceSource source, TraceDefinitionBuilder builder)
    {
        if (!builder.Units.TryGetValue(source.NodeId, out var unit))
        {
            return null;
        }

        var kind = source.Kind == TraceSourceKind.Index ? TraceVisualKind.Index : TraceVisualKind.Allocation;

        var title = source.Role == TraceSourceRole.None ? DisplayName(unit) : $"{source.Role}: {DisplayName(unit)}";

        return new TraceVisualViewModel(kind, database, unit, indexService, title, source.NodeId)
        {
            ShowObjectBorderImmediately = source.Kind == TraceSourceKind.Heap
        };
    }

    private static string DisplayName(AllocationUnit unit)
        => string.IsNullOrEmpty(unit.IndexName) ? unit.TableName : unit.IndexName;
}

public sealed partial class TraceTabViewModel : ObservableObject
{
    private const int RunStepDelayMs = 150;

    private const int HistoryLimit = 1000;

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
        PlanNode = planNode;
        QueryTime = queryTime;
        ScanMode = scanMode;
        Visuals = visuals;
        Layout = layout;

        VisualsByNode = visuals.ToDictionary(v => v.NodeId);

        Operators = layout.Tabs;

        RowBuilder = new TraceRowBuilder(layout.Definitions, layout.Sides);

        RootOutput = layout.Streams[definition.NodeId];

        RootOutput.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ResultsLabel));

        SelectedVisual = visuals[0];

        Dock = BuildDock();

        Dock.LayoutChanged += (_, _) => OnDockLayoutChanged();
    }

    public SvgImageSource? IconSource => PlanNode is null ? null : new SvgImageSource(PlanIconResolver.Resolve(PlanNode));

    public DockLayoutViewModel Dock { get; }

    /// <summary>
    /// Lays the trace out flat, one tab per operator beside the panels that describe the walk
    /// </summary>
    /// <remarks>
    /// The definition tree says which operators there are and what each one reads, and nothing more - an operator that reads another shows
    /// that operator's results in the pane it reads them from, while the operator itself is a tab of its own. Nesting the layout instead
    /// buries the inner operators, and the deeper the tree the less of either is left to see.
    /// </remarks>
    private DockLayoutViewModel BuildDock()
    {
        var steps = DocumentViewModel.Create<TraceStepsPanelView>("Trace", this, canClose: false, keepAlive: true, key: "Steps");
        var description = DocumentViewModel.Create<TraceDescriptionPanelView>("Description", this, keepAlive: true, key: "Description");
        var strategy = DocumentViewModel.Create<TraceStrategyPanelView>("Strategy", this, keepAlive: true, key: "Strategy");
        var plan = DocumentViewModel.Create<TracePlanPanelView>("Plan", this, keepAlive: true, key: "Plan");

        var results = DocumentViewModel.Create<TraceResultsPanelView>("Results", this, keepAlive: true, key: "Results");

        _resultsDocument = results;

        var right = new TabGroupNode(steps, description, results, strategy, plan);

        var left = new TabGroupNode([.. OperatorDocuments()]);

        _operatorGroup = left;

        left.PropertyChanged += OnOperatorGroupPropertyChanged;

        UpdateActivePlanNodes();

        return new DockLayoutViewModel(new SplitNode(Orientation.Horizontal, left, right));
    }

    private IEnumerable<DocumentViewModel> OperatorDocuments()
        => Operators.Select(o => DocumentViewModel.Create<TraceOperatorPanelView>(OperatorTabTitle(o),
                                                                                  o,
                                                                                  canClose: false,
                                                                                  keepAlive: true,
                                                                                  key: $"Operator{o.NodeId}"));

    /// <summary>
    /// Names an operator's tab, adding its node id only where the plan has more than one operator of that kind
    /// </summary>
    private string OperatorTabTitle(TraceOperatorViewModel operatorViewModel)
        => Operators.Count(o => o.Title == operatorViewModel.Title) > 1
            ? $"{operatorViewModel.Title} ({operatorViewModel.NodeId})"
            : operatorViewModel.Title;

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

    private TraceRowBuilder RowBuilder { get; }

    public TraceRowStreamViewModel RootOutput { get; }

    private Dictionary<int, TraceVisualViewModel> VisualsByNode { get; }

    public IReadOnlyList<TraceOperatorViewModel> Operators { get; }

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

    /// <summary>
    /// The operator whose tab is open, marked in the plan so a tab can be placed in the tree it came from
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<PlanNode> _activePlanNodes = [];

    private Dictionary<int, PlanNode> _planNodesById = [];

    private TabGroupNode? _operatorGroup;

    partial void OnPlanNodeChanged(PlanNode? value)
    {
        TracePlan = value is null ? null : new ExecutionPlan(0) { Root = [value] };

        _planNodesById = [];

        if (value is not null)
        {
            IndexPlanNodes(value);
        }

        UpdateActivePlanNodes();
    }

    private void IndexPlanNodes(PlanNode node)
    {
        _planNodesById[node.NodeId] = node;

        foreach (var child in node.Children)
        {
            IndexPlanNodes(child);
        }
    }

    private void OnOperatorGroupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabGroupNode.SelectedDocument))
        {
            UpdateActivePlanNodes();
        }
    }

    private void UpdateActivePlanNodes()
    {
        if (_operatorGroup?.SelectedDocument?.Content is not TraceOperatorViewModel operatorViewModel)
        {
            ActivePlanNodes = [];

            return;
        }

        ActivePlanNodes = _planNodesById.TryGetValue(operatorViewModel.NodeId, out var node) ? [node] : [];

        if (Layout.VisualByOperator.TryGetValue(operatorViewModel.NodeId, out var visual))
        {
            SelectedVisual = visual;
        }
    }

    partial void OnSelectedVisualChanged(TraceVisualViewModel value)
    {
        OnPropertyChanged(nameof(SelectedStrategy));
        OnPropertyChanged(nameof(SelectedPhase));
        OnPropertyChanged(nameof(SeekDescription));
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(AllocationUnit));
    }

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
    private AccessStep? _currentStep;

    public string SelectedSectionTitle => SelectedVisual.Title;

    /// <summary>
    /// Whether the trace reads from more than one input, which is when a tab needs naming
    /// </summary>
    public bool HasMultipleSources => Visuals.Count > 1;

    /// <summary>
    /// What the join does with a pair that matches on both sides, or null when the trace is not a join
    /// </summary>
    public JoinDecision? JoinRule => Definition is JoinDefinition join ? join.JoinType.Decide(true, true) : null;

    public AccessStrategy? SelectedStrategy => StrategyBySource.GetValueOrDefault(SelectedVisual.NodeId);

    public AccessPhase? SelectedPhase
        => CurrentStep is { } step && step.NodeId == SelectedVisual.NodeId ? step.AccessPhase : null;

    /// <summary>
    /// True while the selected input is a correlated seek that has not yet been bound, so it has no descent to describe
    /// </summary>
    public bool IsSelectedStrategyPending
        => SelectedStrategy is null && IsStepping;

    private Dictionary<int, AccessStrategy?> StrategyBySource { get; } = [];

    public Brush? OuterAccentBrush => Visuals.Count > 1 ? ToBrush(Visuals[0].ObjectColour) : null;

    public Brush? InnerAccentBrush => Visuals.Count > 1 ? ToBrush(Visuals[1].ObjectColour) : null;

    /// <summary>
    /// The colour a step should be marked with, found from the input that produced it
    /// </summary>
    public Brush? AccentFor(int source)
        => Visuals.Count > 1 && VisualsByNode.TryGetValue(source, out var visual) ? ToBrush(visual.ObjectColour) : null;

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

    public string ResultsLabel => RootOutput.Rows.Count > 0 ? $"Results ({RootOutput.Rows.Count:N0})" : "Results";

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

    public AccessPhase? CurrentPhase => CurrentStep?.AccessPhase;

    public AccessCounters CurrentCounters => CurrentStep?.Counters ?? default;

    public bool IsWalkInProgress => IsStepping && !IsStepComplete;

    /// <summary>
    /// The strategy the selected input will use, worked out before the walk starts
    /// </summary>
    /// <remarks>
    /// Once opened the iterator publishes what it actually settled on, which supersedes this. Until then the definition already says
    /// enough to describe the descent, which is what makes the panel useful before anything has been stepped.
    /// </remarks>
    public AccessStrategy? SeekDescription
    {
        get
        {
            if (!Layout.Definitions.TryGetValue(SelectedVisual.NodeId, out var definition))
            {
                return null;
            }

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
    }

    partial void OnCurrentStepChanged(AccessStep? value)
    {
        OnPropertyChanged(nameof(CurrentPhase));
        OnPropertyChanged(nameof(SelectedPhase));
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

        RouteRow(step);

        UpdateStrategies();

        SyncHeldRows();

        SyncHashTables(step);

        GetVisual(step.NodeId)?.Apply(step);

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
                Selection = SelectedVisual.Kind == TraceVisualKind.Allocation ? PageReadSelection.Last : PageReadSelection.Next
            });

            _hasNavigatedSinceReset = true;
        }

        if (step is AccessStep.Stopped && step.NodeId == Definition.NodeId)
        {
            IsStepComplete = true;
            IsRunning = false;
        }

        UpdateActiveVisual(step);
    }

    private void UpdateActiveVisual(AccessStep? step)
    {
        var isSideStep = step is not null && Visuals.Any(v => v.NodeId == step.NodeId);

        foreach (var visual in Visuals)
        {
            visual.IsDimmed = Visuals.Count > 1
                              && isSideStep
                              && !IsStepComplete
                              && !IsRunning
                              && !IsRunningToEnd
                              && visual.NodeId != step!.NodeId;
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

            if (Stepper is not { } stepper)
            {
                return;
            }

            var steps = new ObservableCollection<AccessStep>();

            var streamRows = Layout.Streams.Keys.ToDictionary(k => k, _ => new List<IndexRecordModel>());

            var replays = new Dictionary<TraceVisualViewModel, TraceVisualReplay>();

            await Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested
                           && await stepper.StepNextAsync(CancellationToken.None) is not null)
                    {
                    }
                }
                catch (OperationCanceledException)
                {
                }

                foreach (var step in stepper.History)
                {
                    TraceStepRuns.Append(step, steps, HistoryLimit);

                    if (streamRows.TryGetValue(step.NodeId, out var rows) && ToStreamModel(step) is { } model)
                    {
                        rows.Add(model);
                    }
                }

                foreach (var visual in Visuals)
                {
                    replays[visual] = visual.ComputeReplay(stepper.History);
                }
            }, CancellationToken.None);

            StepHistory = steps;

            foreach (var (nodeId, rows) in streamRows)
            {
                Layout.Streams[nodeId].Replace(rows);
            }

            UpdateStrategies();

            foreach (var visual in Visuals)
            {
                visual.ApplyReplay(replays[visual]);
            }

            SyncHeldRows();

            SyncHashTables(stepper.Current);

            CurrentStep = stepper.Current;
            IsStepComplete = stepper.IsComplete;

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

        foreach (var stream in Layout.Streams.Values)
        {
            stream.Clear();
        }

        foreach (var held in Layout.HeldRows.Values)
        {
            held.Reset();
        }

        foreach (var hashTable in Layout.HashTables.Values)
        {
            hashTable.Reset();
        }

        CurrentStep = null;

        StrategyBySource.Clear();

        foreach (var visual in Visuals)
        {
            visual.Reset();
        }

        OnPropertyChanged(nameof(SelectedStrategy));
        OnPropertyChanged(nameof(ResultsLabel));
    }

    private TraceVisualViewModel? GetVisual(int nodeId)
        => VisualsByNode.GetValueOrDefault(nodeId);

    private void RouteRow(AccessStep step)
    {
        if (Layout.Streams.TryGetValue(step.NodeId, out var stream) && ToStreamModel(step) is { } model)
        {
            stream.Add(model);
        }
    }

    private IndexRecordModel? ToStreamModel(AccessStep step)
        => step switch
        {
            AccessStep.JoinEmit emit => RowBuilder.ToJoinedModel(emit),
            AccessStep.TopRow { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            AccessStep.Row { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            _ => null
        };

    private void SyncHeldRows()
    {
        if (Stepper is not { } stepper)
        {
            return;
        }

        foreach (var iterator in Iterators(stepper.Root).OfType<IRowBufferIterator>())
        {
            foreach (var buffer in iterator.Buffers)
            {
                if (Layout.HeldRows.TryGetValue((iterator.NodeId, buffer.InputIndex), out var held))
                {
                    held.Sync(buffer.Rows);
                }
            }
        }
    }

    /// <summary>
    /// Brings every hash match's table up to date with the step just taken
    /// </summary>
    private void SyncHashTables(AccessStep? step)
    {
        foreach (var hashTable in Layout.HashTables.Values)
        {
            hashTable.Sync(step);
        }
    }

    /// <summary>
    /// Binds each hash match's table to the iterator filling it, which the factory builds fresh on every open
    /// </summary>
    private void AttachHashTables()
    {
        if (Stepper is not { } stepper)
        {
            return;
        }

        foreach (var iterator in Iterators(stepper.Root).OfType<IHashTableIterator>())
        {
            if (Layout.HashTables.TryGetValue(iterator.NodeId, out var hashTable))
            {
                hashTable.Attach(iterator);
            }
        }
    }

    /// <summary>
    /// The running operators, the whole tree rather than the one at the top of it
    /// </summary>
    private static IEnumerable<IIterator> Iterators(IIterator iterator)
    {
        yield return iterator;

        switch (iterator)
        {
            case IJoinIterator join:
                foreach (var found in Iterators(join.Outer.Iterator).Concat(Iterators(join.Inner.Iterator)))
                {
                    yield return found;
                }

                break;

            case IUnaryIterator { Input: { } input }:
                foreach (var found in Iterators(input))
                {
                    yield return found;
                }

                break;
        }
    }

    /// <summary>
    /// Takes the strategy each input settled on once it was opened, so a tab can show its own rather than the tree's
    /// </summary>
    /// <remarks>
    /// A correlated inner has no strategy until its first rebind plans a descent, so this is called again as the walk proceeds and leaves
    /// what it already found alone.
    /// </remarks>
    private void UpdateStrategies()
    {
        if (Stepper is not { } stepper)
        {
            return;
        }

        foreach (var iterator in Iterators(stepper.Root))
        {
            if (iterator.Strategy is { } strategy && VisualsByNode.ContainsKey(iterator.NodeId))
            {
                StrategyBySource[iterator.NodeId] = strategy;
            }
        }

        OnPropertyChanged(nameof(SelectedStrategy));
    }

    private async Task StartAsync()
    {
        var evaluationContext = QueryTime is { } queryTime ? new EvaluationContext(queryTime) : EvaluationContext.Now;

        var context = new IteratorContext(Database) { EvaluationContext = evaluationContext };

        var stepper = new IteratorStepper(IteratorFactory.Create(Definition), Definition, context);

        Stepper = stepper;

        await Task.Run(() => stepper.StartAsync(CancellationToken.None));

        UpdateStrategies();

        AttachHashTables();

        IsStepping = true;
    }

    private static IndexRecordModel ToRecordModel(IRecord record)
    {
        return TraceVisualViewModel.ToRecordModel(record);
    }
}
