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
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Row;
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
                                     ScanModeResult? scanMode,
                                     bool wrapInSelect = false)
    {
        var builder = new TraceDefinitionBuilder(resolveUnit, database);

        if (builder.Build(node) is not { } built)
        {
            return null;
        }

        var definition = wrapInSelect
            ? new SelectDefinition(built) { NodeId = -1, OutputList = built.OutputList }
            : built;

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

        var title = source.Role == TraceSourceRole.None
            ? $"{DisplayName(unit)} ({source.NodeId})"
            : $"{source.Role}: {DisplayName(unit)} ({source.NodeId})";

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
            if (layout.Colours.TryGetValue(visual.NodeId, out var colour))
            {
                visual.OperatorColour = colour;
            }
        }

        Operators = layout.Tabs;

        OperatorsByNode = Operators.ToDictionary(o => o.NodeId);

        IndexParents(definition);

        foreach (var op in Operators)
        {
            op.ActivationRequested += ActivateOperator;

            BuildStateItems(op);
        }

        RowBuilder = new TraceRowBuilder(layout.Definitions, layout.Sides);

        SelectedVisual = visuals[0];

        PlanNode = planNode;

        StepNodes = BuildStepNodes();

        Dock = BuildDock();
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
    private DockLayoutViewModel BuildDock() => new(BuildRoot());

    private LayoutNode BuildRoot()
    {
        _stepsDocument ??= DocumentViewModel.Create<TraceStepsPanelView>("Trace", this, canClose: false, keepAlive: true, key: "Steps");
        _descriptionDocument ??= DocumentViewModel.Create<TraceDescriptionPanelView>("Description", this, keepAlive: true, key: "Description");
        _strategyDocument ??= DocumentViewModel.Create<TraceStrategyPanelView>("Strategy", this, keepAlive: true, key: "Strategy");
        _planDocument ??= DocumentViewModel.Create<TracePlanPanelView>("Plan", this, keepAlive: true, key: "Plan");

        _operatorDocumentsByNode ??= Operators.ToDictionary(o => o.NodeId, OperatorDocument);

        var right = new TabGroupNode(_stepsDocument, _descriptionDocument, _strategyDocument, _planDocument);

        LayoutNode left;

        if (IsNestedLayout && BuildNestedNode(Definition) is { } nested)
        {
            _operatorGroup = null;

            left = nested;
        }
        else
        {
            var group = new TabGroupNode([.. Operators.Select(o => _operatorDocumentsByNode[o.NodeId])]);

            _operatorGroup = group;

            group.PropertyChanged += OnOperatorGroupPropertyChanged;

            left = group;
        }

        UpdateActivePlanNodes();

        return new SplitNode(Orientation.Horizontal, left, right);
    }

    private DocumentViewModel? _stepsDocument;

    private DocumentViewModel? _descriptionDocument;

    private DocumentViewModel? _strategyDocument;

    private DocumentViewModel? _planDocument;

    private Dictionary<int, DocumentViewModel>? _operatorDocumentsByNode;

    [ObservableProperty]
    private bool _isNestedLayout;

    partial void OnIsNestedLayoutChanged(bool value)
    {
        if (_operatorGroup is { } group)
        {
            group.PropertyChanged -= OnOperatorGroupPropertyChanged;

            _operatorGroup = null;
        }

        Dock.SetRoot(BuildRoot());
    }

    private LayoutNode? BuildNestedNode(IteratorDefinition definition)
    {
        var document = _operatorDocumentsByNode?.GetValueOrDefault(definition.NodeId);

        var children = OperatorChildren(definition)
                       .Select(BuildNestedNode)
                       .OfType<LayoutNode>()
                       .ToList();

        var childArea = Combine(children);

        LayoutNode? self = document is null ? null : new TabGroupNode(document);

        if (self is null)
        {
            return childArea;
        }

        if (childArea is null)
        {
            return self;
        }

        var isFixedHeight = OperatorsByNode.GetValueOrDefault(definition.NodeId)
            is { IsJoinLayout: false, MainPane.Kind: TracePaneKind.Empty };

        return new SplitNode(Orientation.Vertical, self, childArea)
        {
            FirstStar = 1,
            SecondStar = isFixedHeight ? 3 : 1,
            FirstPixels = definition is SelectDefinition ? 280 : null
        };
    }

    private static LayoutNode? Combine(IReadOnlyList<LayoutNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return null;
        }

        var result = nodes[^1];

        for (var index = nodes.Count - 2; index >= 0; index--)
        {
            result = new SplitNode(Orientation.Horizontal, nodes[index], result)
            {
                FirstStar = 1,
                SecondStar = nodes.Count - 1 - index
            };
        }

        return result;
    }

    private IEnumerable<IteratorDefinition> OperatorChildren(IteratorDefinition definition)
        => ChildrenOf(definition).Where(HasDocument);

    private static IEnumerable<IteratorDefinition> ChildrenOf(IteratorDefinition definition)
        => definition switch
        {
            NestedLoopsDefinition loops => [loops.Outer, loops.Inner],
            MergeJoinDefinition merge => [merge.Outer.Source, merge.Inner.Source],
            HashMatchDefinition hash => [hash.Build.Source, hash.Probe.Source],
            ConcatenationDefinition concatenation => concatenation.Inputs,
            UnaryDefinition unary => [unary.Source],
            _ => []
        };

    private readonly Dictionary<int, int> _parentByNode = [];

    private void IndexParents(IteratorDefinition definition)
    {
        foreach (var child in ChildrenOf(definition))
        {
            _parentByNode[child.NodeId] = definition.NodeId;

            IndexParents(child);
        }
    }

    private bool HasDocument(IteratorDefinition definition)
        => _operatorDocumentsByNode?.ContainsKey(definition.NodeId) == true;

    private DocumentViewModel OperatorDocument(TraceOperatorViewModel op)
    {
        var document = DocumentViewModel.Create<TraceOperatorPanelView>(op.Title,
                                                                        op,
                                                                        canClose: false,
                                                                        keepAlive: true,
                                                                        key: $"Operator{op.NodeId}");

        if (Layout.Colours.TryGetValue(op.NodeId, out var colour))
        {
            document.Accent = Layout.Palette.For(op.NodeId, ToColour(colour));
        }

        return document;
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

    private TraceRowBuilder RowBuilder { get; }

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

    /// <summary>
    /// The operator whose tab is open, marked in the plan so a tab can be placed in the tree it came from
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<PlanNode> _activePlanNodes = [];

    public PlanNode? ActivePlanNode => ActivePlanNodes.Count > 0 ? ActivePlanNodes[0] : null;

    partial void OnActivePlanNodesChanged(IReadOnlyList<PlanNode> value) => OnPropertyChanged(nameof(ActivePlanNode));

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

        if (Layout is not null)
        {
            StepNodes = BuildStepNodes();
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

        ActivePlanNodes = _planNodesById.TryGetValue(operatorViewModel.NodeId, out var node)
            ? [node]
            : operatorViewModel.NodeId < 0 && PlanNode is { } root ? [root] : [];

        if (Layout.VisualByOperator.TryGetValue(operatorViewModel.NodeId, out var visual))
        {
            SelectedVisual = visual;
        }
    }

    public void ActivateOperator(PlanNode? node)
    {
        if (node is null)
        {
            return;
        }

        var operatorIds = Operators.Select(o => o.NodeId).ToHashSet();

        var target = node;

        while (target is not null && !operatorIds.Contains(target.NodeId))
        {
            target = FindParent(PlanNode, target);
        }

        var targetId = target?.NodeId ?? (operatorIds.Contains(-1) ? -1 : (int?)null);

        if (targetId is not null)
        {
            SelectDocumentFor(targetId.Value);
        }
    }

    public void ActivateOperator(int nodeId)
    {
        if (SelectDocumentFor(nodeId))
        {
            return;
        }

        if (_planNodesById.TryGetValue(nodeId, out var node))
        {
            ActivateOperator(node);
        }
    }

    private bool SelectDocumentFor(int nodeId)
    {
        if (_operatorDocumentsByNode?.GetValueOrDefault(nodeId) is not { } document)
        {
            return false;
        }

        if (Dock.FindGroup(document) is not { } group)
        {
            return false;
        }

        group.SelectedDocument = document;

        return true;
    }

    private static PlanNode? FindParent(PlanNode? root, PlanNode target)
    {
        if (root is null)
        {
            return null;
        }

        foreach (var child in root.Children)
        {
            if (ReferenceEquals(child, target))
            {
                return root;
            }

            if (FindParent(child, target) is { } found)
            {
                return found;
            }
        }

        return null;
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

    [ObservableProperty]
    private IReadOnlyDictionary<int, TraceStepNode>? _stepNodes;

    private IReadOnlyDictionary<int, TraceStepNode> BuildStepNodes()
    {
        var names = new Dictionary<int, string>();

        foreach (var (nodeId, definition) in Layout.Definitions)
        {
            var name = _planNodesById.GetValueOrDefault(nodeId)?.PhysicalOperator
                       ?? TraceLayoutBuilder.DisplayName(definition);

            names[nodeId] = nodeId < 0 ? name : $"{name} ({nodeId})";
        }

        var nodes = new Dictionary<int, TraceStepNode>();

        foreach (var (nodeId, definition) in Layout.Definitions)
        {
            var (outerInput, innerInput) = Layout.InputNodes.GetValueOrDefault(nodeId, (-1, -1));

            nodes[nodeId] = new TraceStepNode(names[nodeId],
                                              Layout.Depths.GetValueOrDefault(nodeId),
                                              ToColour(Layout.Colours.GetValueOrDefault(nodeId, System.Drawing.Color.Gray)),
                                              outerInput,
                                              innerInput,
                                              NodeSummary(nodeId, definition, names),
                                              NodeSubtitle(nodeId));
        }

        return nodes;
    }

    private string NodeSubtitle(int nodeId)
    {
        if (VisualsByNode.GetValueOrDefault(nodeId)?.AllocationUnit is not { } unit)
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(unit.IndexName) || unit.IndexName == unit.TableName
            ? unit.TableName
            : $"{unit.TableName} ({unit.IndexName})";
    }

    private string NodeSummary(int nodeId, IteratorDefinition definition, IReadOnlyDictionary<int, string> names)
    {
        if (definition is TopDefinition top)
        {
            return $"The operator returns the first {top.RowCount:N0} rows from its input, then closes it.";
        }

        if (definition is SortDefinition sort)
        {
            return sort.TopCount is { } topCount
                ? $"The operator collects its input, keeps the top {topCount:N0} rows by the sort keys and outputs them in order."
                : sort.IsDistinct
                    ? "The operator collects its whole input, sorts it and outputs each distinct key once."
                    : "The operator collects its whole input before returning anything, then outputs the rows in sorted order.";
        }

        if (definition is not JoinDefinition join)
        {
            return string.Empty;
        }

        var (outerId, innerId) = Layout.InputNodes.GetValueOrDefault(nodeId, (-1, -1));

        var outer = names.GetValueOrDefault(outerId, "outer");

        var inner = names.GetValueOrDefault(innerId, "inner");

        var rule = join.JoinType switch
        {
            JoinType.Inner
                => $"A row is output when the outer ({outer}) and inner ({inner}) rows match.",
            JoinType.LeftOuter
                => $"Every outer ({outer}) row is output, joined to each matching inner ({inner}) row, or to NULLs where none match.",
            JoinType.RightOuter
                => $"Every inner ({inner}) row is output, joined to each matching outer ({outer}) row, or to NULLs where none match.",
            JoinType.FullOuter
                => $"Matched rows from the outer ({outer}) and inner ({inner}) join, and unmatched rows from either side are "
                   + "output with NULLs.",
            JoinType.LeftSemi
                => $"An outer ({outer}) row is output once if any inner ({inner}) row matches. Inner rows are never output.",
            JoinType.RightSemi
                => $"An inner ({inner}) row is output once if any outer ({outer}) row matches. Outer rows are never output.",
            JoinType.LeftAntiSemi
                => $"An outer ({outer}) row is output only when no inner ({inner}) row matches.",
            JoinType.RightAntiSemi
                => $"An inner ({inner}) row is output only when no outer ({outer}) row matches.",
            _ => string.Empty
        };

        return $"The logical operator is {join.JoinType.ToDisplayName()}. {rule}";
    }

    private static Windows.UI.Color ToColour(System.Drawing.Color colour)
        => Windows.UI.Color.FromArgb(colour.A, colour.R, colour.G, colour.B);

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

    private IReadOnlyCollection<int> PathTo(int nodeId)
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

        RouteRow(step);

        UpdateStrategies();

        UpdateOperatorStates();

        AttachHashTables();

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
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task RunToEnd() => RunUntilAsync(null);

    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task RunTo(string target) => RunUntilAsync(StopCondition(target));

    private Func<AccessStep, bool>? StopCondition(string target)
        => target switch
        {
            "Emit" => step => step.NodeId == Definition.NodeId
                              && step is AccessStep.JoinEmit
                                      or AccessStep.TopRow { EmittedRecord: not null }
                                      or AccessStep.Output { EmittedRecord: not null }
                                      or AccessStep.ConcatRow { EmittedRecord: not null }
                                      or AccessStep.SortRow { EmittedRecord: not null }
                                      or AccessStep.Row { EmittedRecord: not null },
            "Rebind" => step => step is AccessStep.Rebind,
            "Phase" => step => step is AccessStep.JoinStart or AccessStep.Reseek,
            "PageRead" => step => step is AccessStep.ReadPage,
            _ => null
        };

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

            var steps = new ObservableCollection<AccessStep>();

            var lastRows = new Dictionary<int, IndexRecordModel>();

            var accumulated = new Dictionary<int, List<IndexRecordModel>>();

            var replays = new Dictionary<TraceVisualViewModel, TraceVisualReplay>();

            await Task.Run(async () =>
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

                foreach (var step in stepper.History)
                {
                    TraceStepRuns.Append(step, steps, HistoryLimit);

                    if (Layout.Streams.TryGetValue(step.NodeId, out var stream) && ToStreamModel(step) is { } model)
                    {
                        if (stream.IsAccumulating)
                        {
                            if (!accumulated.TryGetValue(step.NodeId, out var rows))
                            {
                                rows = [];

                                accumulated[step.NodeId] = rows;
                            }

                            rows.Add(model);
                        }
                        else
                        {
                            lastRows[step.NodeId] = model;
                        }
                    }
                }

                foreach (var visual in Visuals)
                {
                    replays[visual] = visual.ComputeReplay(stepper.History);
                }
            }, CancellationToken.None);

            StepHistory = steps;

            foreach (var (nodeId, stream) in Layout.Streams)
            {
                if (stream.IsAccumulating)
                {
                    stream.Load(accumulated.GetValueOrDefault(nodeId) ?? []);
                }
                else if (lastRows.TryGetValue(nodeId, out var last))
                {
                    stream.Show(last);
                }
                else
                {
                    stream.Clear();
                }
            }

            UpdateStrategies();

            UpdateOperatorStates();

            foreach (var visual in Visuals)
            {
                visual.ApplyReplay(replays[visual]);
            }

            AttachHashTables();

            SyncHeldRows();

            SyncHashTables(stepper.Current);

            CurrentStep = stepper.Current;
            IsStepComplete = stepper.IsComplete;
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

        foreach (var op in Operators)
        {
            op.StateItems.Clear();

            BuildStateItems(op);

            foreach (var row in op.InputRows)
            {
                row.RowCount = "0";
            }
        }

        StrategyBySource.Clear();

        foreach (var visual in Visuals)
        {
            visual.Reset();
        }

        OnPropertyChanged(nameof(SelectedStrategy));
    }

    private TraceVisualViewModel? GetVisual(int nodeId)
        => VisualsByNode.GetValueOrDefault(nodeId);

    private void RouteRow(AccessStep step)
    {
        if (!Layout.Streams.TryGetValue(step.NodeId, out var stream) || ToStreamModel(step) is not { } model)
        {
            return;
        }

        stream.Show(model);
    }

    private IndexRecordModel? ToStreamModel(AccessStep step)
        => step switch
        {
            AccessStep.JoinEmit emit => RowBuilder.ToJoinedModel(emit),
            AccessStep.TopRow { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            AccessStep.Output { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            AccessStep.ConcatRow { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            AccessStep.SortRow { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            AccessStep.Row { EmittedRecord: { } emitted } => ToRecordModel(emitted),
            _ => null
        };

    private void BuildStateItems(TraceOperatorViewModel tab)
    {
        if (!Layout.Definitions.TryGetValue(tab.NodeId, out var definition))
        {
            return;
        }

        switch (definition)
        {
            case SortDefinition sort:
                if (sort.TopCount is { } sortTarget)
                {
                    tab.StateItems.Add(new TraceStateItem("Target") { Value = sortTarget.ToString("N0") });
                }

                tab.StateItems.Add(new TraceStateItem("Distinct") { Flag = sort.IsDistinct });
                tab.StateItems.Add(new TraceStateItem("Collected") { Value = "0" });
                tab.StateItems.Add(new TraceStateItem("Output") { Value = "0" });
                break;

            case TopDefinition top:
                tab.StateItems.Add(new TraceStateItem("Target") { Value = top.RowCount.ToString("N0") });
                tab.StateItems.Add(new TraceStateItem("Row Count") { Value = "0" });
                break;

            case ConcatenationDefinition concatenation:
                tab.StateItems.Add(new TraceStateItem("Input") { Value = $"1 of {concatenation.Inputs.Count}" });
                tab.StateItems.Add(new TraceStateItem("Rows") { Value = "0" });
                break;
        }
    }

    private void UpdateOperatorStates()
    {
        if (Stepper is not { } stepper)
        {
            return;
        }

        foreach (var iterator in Iterators(stepper.Root))
        {
            if (!OperatorsByNode.TryGetValue(iterator.NodeId, out var tab))
            {
                continue;
            }

            switch (iterator)
            {
                case TopIterator top:
                    tab.SetState("Row Count", top.RowCount.ToString("N0"));
                    break;

                case SortIterator sort:
                    tab.SetState("Collected", sort.CollectedCount.ToString("N0"));
                    tab.SetState("Output", sort.RowCount.ToString("N0"));
                    break;

                case ConcatenationIterator concatenation:
                    tab.SetState("Input", $"{concatenation.InputNumber} of {concatenation.InputCount}");
                    tab.SetState("Rows", concatenation.RowCount.ToString("N0"));
                    break;
            }
        }

        foreach (var tab in Operators)
        {
            foreach (var row in tab.InputRows)
            {
                row.RowCount = stepper.CountersFor(row.SourceNodeId).RowsOutput.ToString("N0");
            }
        }
    }

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
                if (join.Outer?.Iterator is { } outer)
                {
                    foreach (var found in Iterators(outer))
                    {
                        yield return found;
                    }
                }

                if (join.Inner?.Iterator is { } inner)
                {
                    foreach (var found in Iterators(inner))
                    {
                        yield return found;
                    }
                }

                break;

            case IMultiInputIterator multi:
                foreach (var input in multi.Inputs)
                {
                    foreach (var found in Iterators(input))
                    {
                        yield return found;
                    }
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
