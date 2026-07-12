using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

public sealed partial class CallstackDocumentView : UserControl
{
    private readonly Dictionary<CallStackNode, TreeViewNode> _nodes = new();

    private HashSet<CallStackNode>? _visible;

    private bool _revealInfrastructure;

    private string? _startSymbol = "CSQLSource::Execute";

    private bool _operatorsOnly;

    // "Plan Operators" mode: root the tree at the plan's operator hierarchy, each operator holding the call tree of
    // its OWN events (matched by PlanNodeIdentifier) above its child operators, instead of one merged call tree.
    private bool _planMode;

    // The operator rows built in plan mode, so only they are auto-expanded (their call frames stay collapsed).
    private readonly List<TreeViewNode> _operatorNodes = new();

    private CallStackNode? _histogramNode;

    private const string HistogramGrey = "#606060";
    private const string HistogramHighlight = "#4CA3E0";

    private TreeViewNode? _contextNode;

    private QueryViewModel? _viewModel;

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public CallstackDocumentView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();

        StartCombo.SelectedIndex = 0;
    }

    private void OnStartChanged(object sender, SelectionChangedEventArgs e)
    {
        _startSymbol = (StartCombo.SelectedItem as ComboBoxItem)?.Tag as string;

        ApplyScope(_viewModel?.SelectedEvent);
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        _operatorsOnly = QueryOperatorsToggle.IsChecked == true;

        ApplyScope(_viewModel?.SelectedEvent);
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        _planMode = PlanModeToggle.IsChecked == true;

        // The Start root and Query Operators filter only shape the merged tree.
        StartCombo.IsEnabled = !_planMode;
        QueryOperatorsToggle.IsEnabled = !_planMode;

        ApplyScope(_viewModel?.SelectedEvent);
    }

    private void OnNodeRightTapped(object sender, RightTappedRoutedEventArgs e) =>
        _contextNode = (sender as FrameworkElement)?.DataContext as TreeViewNode;

    private void OnExpandAllClick(object sender, RoutedEventArgs e) => SetExpanded(_contextNode, expanded: true);

    private void OnCollapseAllClick(object sender, RoutedEventArgs e) => SetExpanded(_contextNode, expanded: false);

    private static void SetExpanded(TreeViewNode? node, bool expanded)
    {
        if (node is null)
        {
            return;
        }

        node.IsExpanded = expanded;

        foreach (var child in node.Children)
        {
            SetExpanded(child, expanded);
        }
    }

    private void OnViewModelChanged()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnPropertyChanged;
        }

        _viewModel = ViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnPropertyChanged;
        }

        ApplyScope(_viewModel?.SelectedEvent);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A new query (CallStack) always rebuilds. A selection change rescopes the merged tree, but in plan mode it
        // must not — the plan tree is selection-independent, so rebuilding would only reset the user's expansion.
        if (e.PropertyName == nameof(QueryViewModel.CallStack)
            || (e.PropertyName == nameof(QueryViewModel.SelectedEvent) && !_planMode))
        {
            ApplyScope(_viewModel?.SelectedEvent);
        }
    }

    private void ApplyScope(EngineEvent? selected)
    {
        if (_planMode)
        {
            BuildPlanTree();

            return;
        }

        var events = selected is null ? [] : ScopeEvents(selected).ToList();

        var leaves = events.Where(e => e.CallStack is not null).Select(e => e.CallStack!).Distinct().ToList();

        _visible = leaves.Count == 0 ? null : VisibleFrom(leaves);

        _revealInfrastructure = false;

        BuildTree();

        if (_visible is not null && _nodes.Count == 0 && !_operatorsOnly)
        {
            _revealInfrastructure = true;

            BuildTree();
        }

        if (_visible is not null)
        {
            foreach (var treeNode in _nodes.Values)
            {
                treeNode.IsExpanded = true;
            }
        }

        var target = leaves.Count switch
        {
            0 => null,
            1 => leaves[0],
            _ => CommonAncestor(leaves),
        };

        var shown = SelectNode(target);

        SetHistogram(shown, events);
    }

    private void SetHistogram(CallStackNode? node, IReadOnlyList<EngineEvent> events)
    {
        _histogramNode?.DisplayBars = [];

        _histogramNode = node;

        var tree = _viewModel?.CallStack;

        if (node is null || tree is null || tree.ActivityBusiest == 0)
        {
            return;
        }

        var highlight = events.Select(e => tree.BucketOf(e.TimeUs)).ToHashSet();

        node.DisplayBars = node.ActivityCounts
                               .Select((count, bucket) => new ActivityBar(count * tree.ActivityHeight / tree.ActivityBusiest,
                                                                          highlight.Contains(bucket) ? HistogramHighlight : HistogramGrey))
                               .ToList();
    }

    private static HashSet<CallStackNode> VisibleFrom(List<CallStackNode> leaves)
    {
        var visible = new HashSet<CallStackNode>();

        foreach (var leaf in leaves)
        {
            for (var node = leaf; node is { IsRoot: false }; node = node.Parent)
            {
                visible.Add(node);
            }
        }

        return visible;
    }

    private static CallStackNode? CommonAncestor(List<CallStackNode> leaves)
    {
        var common = AncestorsAndSelf(leaves[0]).ToHashSet();

        for (var i = 1; i < leaves.Count; i++)
        {
            common.IntersectWith(AncestorsAndSelf(leaves[i]));
        }

        return common.OrderByDescending(DepthOf).FirstOrDefault();
    }

    private static IEnumerable<CallStackNode> AncestorsAndSelf(CallStackNode node)
    {
        for (var current = node; current is { IsRoot: false }; current = current.Parent)
        {
            yield return current;
        }
    }

    private static int DepthOf(CallStackNode node)
    {
        var depth = 0;

        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            depth++;
        }

        return depth;
    }

    private CallStackNode? SelectNode(CallStackNode? target)
    {
        for (var node = target; node is { IsRoot: false }; node = node.Parent)
        {
            if (_nodes.TryGetValue(node, out var treeNode))
            {
                Tree.SelectedNode = treeNode;

                BringIntoView(treeNode);

                return node;
            }
        }

        return null;
    }

    private void BringIntoView(TreeViewNode node) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (Tree.ContainerFromNode(node) is FrameworkElement container)
            {
                container.StartBringIntoView();
            }
        });

    private IEnumerable<EngineEvent> ScopeEvents(EngineEvent selected)
    {
        if (selected is ExecutionOperatorEvent { PlanNodeIdentifier: { } id })
        {
            return (_viewModel?.Events ?? []).Where(e => e.PlanNodeIdentifier == id);
        }

        if (selected is ReadEventGroup group)
        {
            return group.Events;
        }

        return [selected];
    }

    private void BuildTree()
    {
        _nodes.Clear();

        Tree.RootNodes.Clear();

        foreach (var root in StartRoots())
        {
            foreach (var treeNode in BuildVisible(root))
            {
                Tree.RootNodes.Add(treeNode);
            }
        }
    }

    private IEnumerable<CallStackNode> StartRoots()
    {
        var roots = _viewModel?.CallStackRoots ?? [];

        if (_visible is not null || string.IsNullOrEmpty(_startSymbol))
        {
            return roots;
        }

        var found = new List<CallStackNode>();

        foreach (var root in roots)
        {
            FindStart(root, found);
        }

        // Fall back to the top if the start symbol isn't in this query's stacks.
        return found.Count > 0 ? found : roots;
    }

    private void FindStart(CallStackNode node, List<CallStackNode> found)
    {
        if (node.Symbol == _startSymbol)
        {
            found.Add(node);

            return;
        }

        foreach (var child in node.ChildNodes)
        {
            FindStart(child, found);
        }
    }

    private IEnumerable<TreeViewNode> BuildVisible(CallStackNode node)
    {
        var children = node.ChildNodes
                           .OrderBy(child => child.Order)
                           .SelectMany(BuildVisible)
                           .ToList();

        var outOfScope = _visible is not null && !_visible.Contains(node);

        var infrastructureHidden = node.IsInfrastructure && !_revealInfrastructure;

        var nonOperatorHidden = _operatorsOnly && !node.HasOperator;

        if (outOfScope || infrastructureHidden || nonOperatorHidden)
        {
            foreach (var child in children)
            {
                yield return child;
            }

            yield break;
        }

        var treeNode = new TreeViewNode { Content = node };

        _nodes[node] = treeNode;

        foreach (var child in children)
        {
            treeNode.Children.Add(child);
        }

        yield return treeNode;
    }

    // Plan Operators mode: root the tree at the plan's operators. Each operator row carries the call tree of its own
    // events, then its child operators; an operator whose whole subtree captured no call stacks is dropped.
    private void BuildPlanTree()
    {
        _nodes.Clear();
        _operatorNodes.Clear();
        Tree.RootNodes.Clear();

        if (_histogramNode is not null)
        {
            _histogramNode.DisplayBars = [];
            _histogramNode = null;
        }

        var operators = (_viewModel?.Events ?? [])
            .OfType<ExecutionOperatorEvent>()
            .Where(o => o.PlanNodeIdentifier is not null)
            .ToList();

        if (operators.Count == 0)
        {
            return;
        }

        var childrenByParent = operators
            .Where(o => o.ParentNodeId is not null)
            .ToLookup(o => (o.PlanNodeIdentifier!.PlanHandleId, o.ParentNodeId!.Value));

        var nodeIds = operators.Select(o => o.PlanNodeIdentifier!.NodeId).ToHashSet();

        // Roots are operators with no parent in the captured set — the statement node, or the top operators when the
        // statement node itself was not built.
        var roots = operators
            .Where(o => o.ParentNodeId is null || !nodeIds.Contains(o.ParentNodeId.Value))
            .OrderBy(o => o.PlanNodeIdentifier!.NodeId);

        foreach (var root in roots)
        {
            if (BuildOperatorNode(root, childrenByParent) is { } node)
            {
                Tree.RootNodes.Add(node);
            }
        }

        // Expand the operator hierarchy once the nodes are attached (WinUI can drop a subtree expanded while detached);
        // the call frames under each operator stay collapsed so the plan stays readable.
        foreach (var operatorNode in _operatorNodes)
        {
            operatorNode.IsExpanded = true;
        }
    }

    // A tree node for an operator: its own call tree followed by its child operators, or null when neither it nor any
    // descendant captured a call stack (so empty operators do not clutter the plan).
    private TreeViewNode? BuildOperatorNode(
        ExecutionOperatorEvent op,
        ILookup<(short PlanHandleId, int NodeId), ExecutionOperatorEvent> childrenByParent)
    {
        var callNodes = BuildOperatorCallTree(op);

        var key = (op.PlanNodeIdentifier!.PlanHandleId, op.PlanNodeIdentifier.NodeId);

        var childOperatorNodes = childrenByParent[key]
            .OrderBy(child => child.PlanNodeIdentifier!.NodeId)
            .Select(child => BuildOperatorNode(child, childrenByParent))
            .OfType<TreeViewNode>()
            .ToList();

        if (callNodes.Count == 0 && childOperatorNodes.Count == 0)
        {
            return null;
        }

        var operatorNode = new TreeViewNode { Content = op };

        foreach (var callNode in callNodes)
        {
            operatorNode.Children.Add(callNode);
        }

        foreach (var childOperatorNode in childOperatorNodes)
        {
            operatorNode.Children.Add(childOperatorNode);
        }

        _operatorNodes.Add(operatorNode);

        return operatorNode;
    }

    // The scoped call tree for an operator's OWN events (infrastructure hidden), or an empty list when it captured no
    // stacks. Falls back to revealing infrastructure if hiding it would leave the operator's paths empty.
    private List<TreeViewNode> BuildOperatorCallTree(ExecutionOperatorEvent op)
    {
        var leaves = OperatorLeaves(op);

        if (leaves.Count == 0)
        {
            return [];
        }

        var visible = VisibleFrom(leaves);

        var nodes = ScopedCallNodes(visible, revealInfrastructure: false);

        return nodes.Count > 0 ? nodes : ScopedCallNodes(visible, revealInfrastructure: true);
    }

    private List<TreeViewNode> ScopedCallNodes(HashSet<CallStackNode> visible, bool revealInfrastructure)
    {
        var nodes = new List<TreeViewNode>();

        foreach (var root in _viewModel?.CallStackRoots ?? [])
        {
            nodes.AddRange(BuildScopedCall(root, visible, revealInfrastructure));
        }

        return nodes;
    }

    // The leaf call-stack nodes of an operator's own events (read groups expanded to their children), deduplicated.
    private List<CallStackNode> OperatorLeaves(ExecutionOperatorEvent op)
    {
        var id = op.PlanNodeIdentifier;

        var leaves = new List<CallStackNode>();

        foreach (var e in _viewModel?.Events ?? [])
        {
            if (e is ExecutionOperatorEvent || e.PlanNodeIdentifier != id)
            {
                continue;
            }

            if (e is ReadEventGroup group)
            {
                leaves.AddRange(group.Events.Where(c => c.CallStack is not null).Select(c => c.CallStack!));
            }
            else if (e.CallStack is not null)
            {
                leaves.Add(e.CallStack);
            }
        }

        return leaves.Distinct().ToList();
    }

    // Renders a call-stack node's subtree scoped to visible, hiding infrastructure (promoting its visible children) and
    // out-of-scope frames — the same projection BuildVisible does, but for a supplied scope set.
    private IEnumerable<TreeViewNode> BuildScopedCall(
        CallStackNode node,
        HashSet<CallStackNode> visible,
        bool revealInfrastructure)
    {
        var children = node.ChildNodes
                           .OrderBy(child => child.Order)
                           .SelectMany(child => BuildScopedCall(child, visible, revealInfrastructure))
                           .ToList();

        var outOfScope = !visible.Contains(node);

        var infrastructureHidden = node.IsInfrastructure && !revealInfrastructure;

        if (outOfScope || infrastructureHidden)
        {
            foreach (var child in children)
            {
                yield return child;
            }

            yield break;
        }

        var treeNode = new TreeViewNode { Content = node };

        foreach (var child in children)
        {
            treeNode.Children.Add(child);
        }

        yield return treeNode;
    }
}
