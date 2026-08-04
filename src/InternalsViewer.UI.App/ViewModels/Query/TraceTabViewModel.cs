using System.Collections.Generic;
using System.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records;
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
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;

namespace InternalsViewer.UI.App.ViewModels.Query;

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

        return new TraceTabViewModel(iteratorFactory, definition, database, node, queryTime, scanMode, visuals);
    }

    private TraceVisualViewModel? CreateVisual(DatabaseSource database, TraceSource source, TraceDefinitionBuilder builder)
    {
        if (!builder.Units.TryGetValue(source.NodeId, out var unit))
        {
            return null;
        }

        var kind = source.Kind == TraceSourceKind.Index ? TraceVisualKind.Index : TraceVisualKind.Allocation;

        var title = source.Role == TraceSourceRole.None ? DisplayName(unit) : $"{source.Role}: {DisplayName(unit)}";

        var node = builder.Nodes.GetValueOrDefault(source.NodeId);

        return new TraceVisualViewModel(kind, database, unit, indexService, title, source.NodeId)
        {
            IsSideStackVisible = source.Role != TraceSourceRole.None,
            ShowObjectBorderImmediately = source.Kind == TraceSourceKind.Heap,
            IsHashTableVisible = source.Role == TraceSourceRole.Build,
            OperatorNodeId = source.OperatorNodeId,
            InputIndex = InputIndexOf(source.Role),
            ColumnFilter = RecordColumnFilter.For(node, includesBookmark: source.Role == TraceSourceRole.Lookup)
        };
    }

    /// <summary>
    /// Which of its operator's two inputs a role is, the rows a join holds being kept per input
    /// </summary>
    private static int InputIndexOf(TraceSourceRole role)
        => role switch
        {
            TraceSourceRole.Outer or TraceSourceRole.Build or TraceSourceRole.Seek => 0,
            TraceSourceRole.Inner or TraceSourceRole.Probe or TraceSourceRole.Lookup => 1,
            _ => -1
        };

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
                             IReadOnlyList<TraceVisualViewModel> visuals)
    {
        IteratorFactory = iteratorFactory;
        Definition = definition;
        Database = database;
        PlanNode = planNode;
        QueryTime = queryTime;
        ScanMode = scanMode;
        Visuals = visuals;
        VisualsBySource = visuals.ToDictionary(v => v.Source);
        DefinitionBySource = TraceSourceCollector.Collect(definition).ToDictionary(s => s.NodeId, s => s.Definition);

        Operators = [.. TraceSourceCollector.CollectOperators(definition)
                                            .Select(o => new TraceOperatorViewModel(o.NodeId, OperatorTitle(o), OperatorDescription(o)))];

        OperatorsBySource = Operators.ToDictionary(o => o.NodeId);

        BuildOperatorPanes();

        SelectedVisual = visuals[0];

        Dock = BuildDock();

        Dock.LayoutChanged += (_, _) => OnDockLayoutChanged();

        Strategy = SeekDescription;
    }

    private static string OperatorTitle(JoinDefinition definition)
        => definition switch
        {
            HashMatchDefinition => "Hash Match",
            MergeJoinDefinition => "Merge Join",
            NestedLoopsDefinition => "Nested Loops",
            _ => "Results"
        };

    /// <summary>
    /// The join type and the columns matched on, stated the way the operator states them
    /// </summary>
    private static string OperatorDescription(JoinDefinition definition)
    {
        var keys = definition switch
        {
            HashMatchDefinition hash => hash.Build.JoinColumns,
            MergeJoinDefinition merge => merge.Outer.JoinColumns,
            _ => []
        };

        var on = keys.Count > 0 ? $" on {string.Join(", ", keys)}" : string.Empty;

        var residual = definition.Residual is null ? string.Empty : " with residual";

        return $"{definition.JoinType.ToDisplayName()}{on}{residual}";
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

    /// <summary>
    /// One document per operator, or the input itself when the trace is of a single access path
    /// </summary>
    private IEnumerable<DocumentViewModel> OperatorDocuments()
    {
        if (Operators.Count == 0)
        {
            return Visuals.Select(v => VisualDocument(v.Source));
        }

        return Operators.Select(o => DocumentViewModel.Create<TraceOperatorPanelView>(OperatorTabTitle(o),
                                                                                      o,
                                                                                      canClose: false,
                                                                                      keepAlive: true,
                                                                                      key: $"Operator{o.NodeId}"));
    }

    /// <summary>
    /// Names an operator's tab, adding its node id only where the plan has more than one operator of that kind
    /// </summary>
    private string OperatorTabTitle(TraceOperatorViewModel operatorViewModel)
        => Operators.Count(o => o.Title == operatorViewModel.Title) > 1
            ? $"{operatorViewModel.Title} ({operatorViewModel.NodeId})"
            : operatorViewModel.Title;

    /// <summary>
    /// Fills each operator's panes from the definition tree, which is the only thing that says what an input is
    /// </summary>
    private void BuildOperatorPanes()
    {
        foreach (var join in TraceSourceCollector.CollectOperators(Definition))
        {
            if (!OperatorsBySource.TryGetValue(join.NodeId, out var operatorViewModel))
            {
                continue;
            }

            var (outer, inner) = Inputs(join);

            SidesByOperator[join.NodeId] = new OperatorSides(OperatorNodeIdOf(outer),
                                                             OperatorNodeIdOf(inner),
                                                             TablesUnder(outer),
                                                             TablesUnder(inner));

            var hashTable = join is HashMatchDefinition ? CreateHashTable(join.NodeId, outer) : null;

            operatorViewModel.OuterTop = InputPane(outer);
            operatorViewModel.OuterBottom = HeldPane(outer, hashTable);

            operatorViewModel.InnerTop = InputPane(inner);
            operatorViewModel.InnerBottom = HeldPane(inner, hashTable: null);
        }
    }

    /// <summary>
    /// Gives a hash match a table of its own, showing the build rows as the columns that side states it outputs
    /// </summary>
    /// <remarks>
    /// A build side that is another operator hands up a row carrying every column both of its own sides read, so the columns come from
    /// that operator's output list. Taking the record as it stands would show the workings of the whole subtree.
    /// </remarks>
    private TraceHashTableViewModel CreateHashTable(int nodeId, IteratorDefinition? build)
    {
        var input = Unwrap(build);

        var filter = input switch
        {
            JoinDefinition join => RecordColumnFilter.For(_planNodesById.GetValueOrDefault(join.NodeId)),
            not null when VisualsBySource.TryGetValue(input.NodeId, out var visual) => visual.ColumnFilter,
            _ => RecordColumnFilter.All
        };

        var hashTable = new TraceHashTableViewModel(filter);

        HashTables[nodeId] = hashTable;

        return hashTable;
    }

    /// <summary>
    /// The operator on one side of a join, or -1 where that side reads an object directly
    /// </summary>
    private int OperatorNodeIdOf(IteratorDefinition? side)
        => Unwrap(side) is JoinDefinition join ? join.NodeId : -1;

    /// <summary>
    /// The objects read anywhere below one side of a join, which is what says whose column an operator is asked for
    /// </summary>
    private HashSet<string> TablesUnder(IteratorDefinition? side)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (side is null)
        {
            return tables;
        }

        foreach (var source in TraceSourceCollector.Collect(side))
        {
            if (VisualsBySource.TryGetValue(source.NodeId, out var visual) && visual.AllocationUnit.TableName is { } table)
            {
                tables.Add(table);
            }
        }

        return tables;
    }

    private static (IteratorDefinition? Outer, IteratorDefinition? Inner) Inputs(JoinDefinition join)
        => join switch
        {
            NestedLoopsDefinition loops => (loops.Outer, loops.Inner),
            MergeJoinDefinition merge => (merge.Outer.Source, merge.Inner.Source),
            HashMatchDefinition hash => (hash.Build.Source, hash.Probe.Source),
            _ => (null, null)
        };

    /// <summary>
    /// What one side of an operator shows, which is its object when it reads one and otherwise the results of the operator below
    /// </summary>
    private TracePane InputPane(IteratorDefinition? definition)
    {
        var input = Unwrap(definition);

        if (input is JoinDefinition join && OperatorsBySource.TryGetValue(join.NodeId, out var operatorViewModel))
        {
            return new TracePane(TracePaneKind.OperatorResults, operatorViewModel);
        }

        if (input is not null && VisualsBySource.TryGetValue(input.NodeId, out var visual))
        {
            return new TracePane(TracePaneKind.Visual, visual, visual.Title);
        }

        return TracePane.Empty;
    }

    /// <summary>
    /// What one side holds of what it has read, which for the build side of a hash match is the table it filled
    /// </summary>
    /// <remarks>
    /// The table is the operator's, so a hash match shows one whether its build side read an object or another operator. Any other side
    /// reading an operator holds nothing of its own, its rows being that operator's results, which the pane above already shows.
    /// </remarks>
    private TracePane HeldPane(IteratorDefinition? definition, TraceHashTableViewModel? hashTable)
    {
        if (hashTable is not null)
        {
            return new TracePane(TracePaneKind.HashTable, hashTable);
        }

        var input = Unwrap(definition);

        if (input is null or JoinDefinition || !VisualsBySource.TryGetValue(input.NodeId, out var visual))
        {
            return TracePane.Empty;
        }

        return visual.IsSideStackVisible ? new TracePane(TracePaneKind.Records, visual) : TracePane.Empty;
    }

    private static IteratorDefinition? Unwrap(IteratorDefinition? definition)
        => definition is UnaryDefinition unary ? Unwrap(unary.Source) : definition;

    private DocumentViewModel VisualDocument(int source)
    {
        var visual = VisualsBySource[source];

        return DocumentViewModel.Create<TraceVisualPanelView>(visual.Title,
                                                              visual,
                                                              canClose: false,
                                                              keepAlive: true,
                                                              key: $"Visual{source}");
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

    private Dictionary<int, TraceVisualViewModel> VisualsBySource { get; }

    public IReadOnlyList<TraceOperatorViewModel> Operators { get; }

    private Dictionary<int, TraceOperatorViewModel> OperatorsBySource { get; }

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
        ActivePlanNodes = _operatorGroup?.SelectedDocument?.Content is TraceOperatorViewModel operatorViewModel
                          && _planNodesById.TryGetValue(operatorViewModel.NodeId, out var node)
            ? [node]
            : [];
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
    private ObservableCollection<IndexRecordModel> _resultRecords = [];


    [ObservableProperty]
    private AccessStep? _currentStep;

    [ObservableProperty]
    private AccessStrategy? _strategy;

    [ObservableProperty]
    private AccessStrategy? _innerStrategy;

    public string SelectedSectionTitle => SelectedVisual.Title;

    /// <summary>
    /// Whether the trace reads from more than one input, which is when a tab needs naming
    /// </summary>
    public bool HasMultipleSources => Visuals.Count > 1;

    /// <summary>
    /// What the join does with a pair that matches on both sides, or null when the trace is not a join
    /// </summary>
    public JoinDecision? JoinRule => Definition is JoinDefinition join ? join.JoinType.Decide(true, true) : null;

    public AccessStrategy? SelectedStrategy => StrategyBySource.GetValueOrDefault(SelectedVisual.Source);

    public AccessPhase? SelectedPhase
        => CurrentStep is { } step && step.NodeId == SelectedVisual.Source ? step.AccessPhase : null;

    /// <summary>
    /// True while the selected input is a correlated seek that has not yet been bound, so it has no descent to describe
    /// </summary>
    public bool IsSelectedStrategyPending
        => SelectedStrategy is null && IsStepping;

    private Dictionary<int, AccessStrategy?> StrategyBySource { get; } = [];

    private Dictionary<int, IteratorDefinition> DefinitionBySource { get; }

    /// <summary>
    /// The table each hash match of the trace fills, by the node id of the operator that owns it
    /// </summary>
    private Dictionary<int, TraceHashTableViewModel> HashTables { get; } = [];

    /// <summary>
    /// What sits on each side of every operator, which is how a column an operator outputs is traced back to the row holding it
    /// </summary>
    private Dictionary<int, OperatorSides> SidesByOperator { get; } = [];

    private sealed record OperatorSides(int OuterNodeId, int InnerNodeId, HashSet<string> OuterTables, HashSet<string> InnerTables);

    public Brush? OuterAccentBrush => Visuals.Count > 1 ? ToBrush(Visuals[0].ObjectColour) : null;

    public Brush? InnerAccentBrush => Visuals.Count > 1 ? ToBrush(Visuals[1].ObjectColour) : null;

    /// <summary>
    /// The colour a step should be marked with, found from the input that produced it
    /// </summary>
    public Brush? AccentFor(int source)
        => Visuals.Count > 1 && VisualsBySource.TryGetValue(source, out var visual) ? ToBrush(visual.ObjectColour) : null;

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
            if (!DefinitionBySource.TryGetValue(SelectedVisual.Source, out var definition))
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

    private ScanDirection ScanDirection
        => PlanNode?.ScanInfo?.IsForward == false ? ScanDirection.Backward : ScanDirection.Forward;

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

        Append(step, StepHistory);

        if (step is AccessStep.JoinEmit pair && OperatorsBySource.TryGetValue(step.NodeId, out var emitting))
        {
            emitting.Add(ToJoinedModel(pair));
        }

        if (ToResultModel(step) is { } resultModel)
        {
            ResultRecords.Add(resultModel);

            UpdateResultsTitle();
        }

        UpdateStrategies();

        SyncSideBuffers();

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
        var isSideStep = step is not null && Visuals.Any(v => v.Source == step.NodeId);

        foreach (var visual in Visuals)
        {
            visual.IsDimmed = Visuals.Count > 1
                              && isSideStep
                              && !IsStepComplete
                              && !IsRunning
                              && !IsRunningToEnd
                              && visual.Source != step!.NodeId;
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

            var stepper = Stepper!;

            var steps = new ObservableCollection<AccessStep>();

            var results = new ObservableCollection<IndexRecordModel>();

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
                    Append(step, steps);

                    if (ToResultModel(step) is { } resultModel)
                    {
                        results.Add(resultModel);
                    }
                }

                foreach (var visual in Visuals)
                {
                    replays[visual] = visual.ComputeReplay(stepper.History);
                }
            }, CancellationToken.None);

            StepHistory = steps;
            ResultRecords = results;

            UpdateResultsTitle();
            UpdateStrategies();

            foreach (var visual in Visuals)
            {
                visual.ApplyReplay(replays[visual]);
            }

            SyncSideBuffers();

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
        ResultRecords = [];

        foreach (var operatorViewModel in Operators)
        {
            operatorViewModel.Clear();
        }

        foreach (var hashTable in HashTables.Values)
        {
            hashTable.Reset();
        }

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
    /// Refreshes each side's row pane from the rows the join reading it is currently holding
    /// </summary>
    /// <remarks>
    /// Taken from the join rather than accumulated from the row steps, because only the join knows which rows it still holds - a row it
    /// has advanced past is gone, and a row read ahead of the current key is not yet in play. Which buffer a side reads from is settled by
    /// the operator it feeds and which of that operator's two inputs it is, so a trace holding more than one join keeps them apart.
    /// </remarks>
    private void SyncSideBuffers()
    {
        if (Stepper is not { } stepper)
        {
            return;
        }

        foreach (var join in Iterators(stepper.Root).OfType<IJoinIterator>())
        {
            SyncSideBuffer(join.NodeId, 0, join.Outer);
            SyncSideBuffer(join.NodeId, 1, join.Inner);
        }
    }

    private void SyncSideBuffer(int operatorNodeId, int inputIndex, IJoinInput input)
    {
        var visual = Visuals.FirstOrDefault(v => v.OperatorNodeId == operatorNodeId && v.InputIndex == inputIndex);

        if (visual is null || !visual.IsSideStackVisible || visual.IsHashTableVisible)
        {
            return;
        }

        var buffer = input.Buffer;

        if (_syncedBuffers.TryGetValue(visual.Source, out var synced) && synced.SequenceEqual(buffer))
        {
            return;
        }

        _syncedBuffers[visual.Source] = [.. buffer];

        Refill(visual.SideRecords, buffer);
    }

    /// <summary>
    /// Rebuilds a row pane from the buffer, in the collection the grid is already bound to
    /// </summary>
    /// <remarks>
    /// Handing the grid a new collection rebinds it, which rebuilds its columns and every row container it had realised - the whole pane
    /// rebuilt for a step that moved one row, paid on every step of the walk. Refilling the collection it holds costs none of that. The
    /// rows are rebuilt rather than the changed ones picked out because the grid's collection view does not track an item replaced in
    /// place, and a buffer holds only what a join is working with, which is a row or two.
    /// </remarks>
    private static void Refill(ObservableCollection<IndexRecordModel> records, IReadOnlyList<JoinBufferRow> buffer)
    {
        records.Clear();

        foreach (var row in buffer)
        {
            records.Add(ToRecordModel(row));
        }
    }

    private readonly Dictionary<int, List<JoinBufferRow>> _syncedBuffers = new();

    /// <summary>
    /// Brings every hash match's table up to date with the step just taken
    /// </summary>
    private void SyncHashTables(AccessStep? step)
    {
        foreach (var hashTable in HashTables.Values)
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
            if (HashTables.TryGetValue(iterator.NodeId, out var hashTable))
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

    private static IndexRecordModel ToRecordModel(JoinBufferRow row)
    {
        var model = ToRecordModel(row.Record);

        model.IsMatched = row.IsMatched;

        return model;
    }

    /// <summary>
    /// The row a step contributes to the trace's results, or null for a step that produced none
    /// </summary>
    /// <remarks>
    /// Only the operator at the top of the tree produces the trace's results. An operator below it feeds the one above rather than the
    /// output, and its rows travel up through the same stream, so a step is matched on the operator that stamped it and never on its kind.
    /// </remarks>
    private IndexRecordModel? ToResultModel(AccessStep step)
    {
        if (step.NodeId != Definition.NodeId)
        {
            return null;
        }

        return step switch
        {
            AccessStep.JoinEmit emit => ToJoinedModel(emit),
            AccessStep.Row { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            AccessStep.TopRow { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            _ => null
        };
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
            if (iterator.Strategy is { } strategy && VisualsBySource.ContainsKey(iterator.NodeId))
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

        Strategy = stepper.Root.Strategy;

        UpdateStrategies();

        AttachHashTables();

        IsStepping = true;
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
        var outputColumns = (_planNodesById.GetValueOrDefault(emit.NodeId) ?? PlanNode)?.OutputColumns ?? [];

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

        var field = FindColumn(emit.OuterRecord, emit.InnerRecord, emit.NodeId, column.Table.Trim('[', ']'), name);

        return new IndexRecordFieldModel
        {
            Name = name,
            Value = field?.Value ?? "NULL",
            DataType = field?.ColumnStructure.DataType ?? SqlDbType.Variant
        };
    }

    /// <summary>
    /// Finds the column an operator states it outputs, following the side of the tree the table it belongs to sits on
    /// </summary>
    /// <remarks>
    /// A name alone cannot say which column is meant once an operator reads another - a join of two tables that both have a Name hands up
    /// a row carrying both, and the operator above states which of them it wants by naming the table. The tables under each side are known
    /// from the definition tree, so the side is settled first and the name resolved within it.
    /// </remarks>
    private RecordField? FindColumn(IRecord? outer, IRecord? inner, int operatorNodeId, string table, string name)
    {
        if (table.Length > 0 && SidesByOperator.TryGetValue(operatorNodeId, out var sides))
        {
            if (sides.OuterTables.Contains(table))
            {
                return Descend(outer, sides.OuterNodeId, table, name) ?? Descend(inner, sides.InnerNodeId, table, name);
            }

            if (sides.InnerTables.Contains(table))
            {
                return Descend(inner, sides.InnerNodeId, table, name) ?? Descend(outer, sides.OuterNodeId, table, name);
            }
        }

        return Find(outer, name) ?? Find(inner, name);
    }

    private RecordField? Descend(IRecord? record, int operatorNodeId, string table, string name)
        => record is JoinedRecord joined
            ? FindColumn(joined.Outer, joined.Inner, operatorNodeId, table, name)
            : Find(record, name);

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

    /// <summary>
    /// Finds the probe of the same key that this one repeats, or null where the last probe was of something else
    /// </summary>
    /// <remarks>
    /// A probe row that found candidates leaves its comparisons and whatever it emitted behind it, and the row that carried the next key
    /// lands on top of those, so the probe before is not the entry beneath. Anything other than that work ends the search, a probe of a
    /// different key most of all, so a run only ever gathers probes that ran one after another.
    /// </remarks>
    private static int? FindProbeRun(ObservableCollection<AccessStep> history, AccessStep.HashProbe probe)
    {
        for (var index = 0; index < history.Count; index++)
        {
            var previous = history[index];

            if (previous is AccessStep.Row or AccessStep.RowRun or AccessStep.HashCompare or AccessStep.JoinEmit)
            {
                continue;
            }

            var key = previous switch
            {
                AccessStep.HashProbe found => found.Key,
                AccessStep.HashProbeRun run => run.Key,
                _ => (AccessKey?)null
            };

            return key is { } previousKey && previous.NodeId == probe.NodeId && previousKey.Equals(probe.Key) ? index : null;
        }

        return null;
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
                    NodeId = compare.NodeId,
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

        if (step is AccessStep.HashProbe hashProbe && FindProbeRun(history, hashProbe) is { } probeIndex)
        {
            history[probeIndex] = history[probeIndex] is AccessStep.HashProbeRun hashProbeRun
                ? hashProbeRun with
                {
                    Count = hashProbeRun.Count + 1,
                    Counters = hashProbe.Counters
                }
                : new AccessStep.HashProbeRun(hashProbe.Bucket, hashProbe.Hash, 2)
                {
                    Key = hashProbe.Key,
                    ChainLength = hashProbe.ChainLength,
                    IsNullKey = hashProbe.IsNullKey,
                    NodeId = hashProbe.NodeId,
                    Counters = hashProbe.Counters
                };

            return;
        }

        if (step is AccessStep.Probe probe)
        {
            if (history.Count > 0 && history[0] is AccessStep.ProbeRun probeRun && probeRun.NodeId == probe.NodeId)
            {
                history[0] = new AccessStep.ProbeRun([probe, .. probeRun.Probes])
                {
                    NodeId = probe.NodeId,
                    Counters = probe.Counters
                };

                return;
            }

            step = new AccessStep.ProbeRun([probe])
            {
                NodeId = probe.NodeId,
                Counters = probe.Counters
            };
        }

        if (step is AccessStep.Row row && history.Count > 0)
        {
            var latest = history[0];

            if (latest is AccessStep.Row previous
                && previous.NodeId == row.NodeId
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
                    NodeId = row.NodeId
                };

                return;
            }

            if (latest is AccessStep.RowRun run
                && run.NodeId == row.NodeId
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
