using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

public sealed partial class CallstackDocumentView : UserControl
{
    // Maps each call stack node to its tree node, so a selected event's leaf can be expanded to and selected.
    private readonly Dictionary<CallStackNode, TreeViewNode> _nodes = new();

    // The nodes to show: null shows the whole (collapsed) tree; otherwise only the selection's call paths.
    private HashSet<CallStackNode>? _visible;

    // When a scoped selection's whole path is infrastructure, hiding it would leave nothing — so reveal the path's
    // infrastructure frames rather than show an empty tree.
    private bool _revealInfrastructure;

    // The symbol the tree roots at (from the Start dropdown); empty/null roots at the post-infrastructure top.
    private string? _startSymbol = "CSQLSource::Execute";

    // The node currently showing the activity histogram, so it can be cleared when the selection moves.
    private CallStackNode? _histogramNode;

    // Histogram bar colours: the selected event's time bucket is highlighted, every other bucket is grey.
    private const string HistogramGrey = "#606060";
    private const string HistogramHighlight = "#4CA3E0";

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
        if (e.PropertyName is nameof(QueryViewModel.CallStack) or nameof(QueryViewModel.SelectedEvent))
        {
            ApplyScope(_viewModel?.SelectedEvent);
        }
    }

    // Scopes the tree to the selected event's (or operator's/group's) call paths so anything else — noise from
    // out-of-scope events — is hidden; with nothing selected the whole tree is shown, collapsed. A single event
    // selects its own leaf; several (an operator or a grouped read) select where they converge — their common
    // ancestor (e.g. reads all share BPool::Get).
    private void ApplyScope(EngineEvent? selected)
    {
        var events = selected is null ? [] : ScopeEvents(selected).ToList();

        var leaves = events.Where(e => e.CallStack is not null).Select(e => e.CallStack!).Distinct().ToList();

        _visible = leaves.Count == 0 ? null : VisibleFrom(leaves);

        _revealInfrastructure = false;

        BuildTree();

        // The selection's whole path can be infrastructure (a data read, a scheduler-side event); hiding it as noise
        // would leave nothing, so reveal the path's infrastructure and rebuild rather than show an empty tree.
        if (_visible is not null && _nodes.Count == 0)
        {
            _revealInfrastructure = true;

            BuildTree();
        }

        // Expand the shown nodes now they are attached to the tree, so a scoped path opens down to the selection.
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

    // Shows the activity histogram on the selected node only: its subtree activity as grey bars, with the bucket(s) the
    // selected event(s) land in highlighted, so you can see where in the query's life this instance ran. Cleared off
    // whatever node last carried it.
    private void SetHistogram(CallStackNode? node, IReadOnlyList<EngineEvent> events)
    {
        if (_histogramNode is not null)
        {
            _histogramNode.DisplayBars = [];
        }

        _histogramNode = node;

        var tree = _viewModel?.CallStack;

        if (node is null || tree is null || tree.ActivityBusiest == 0)
        {
            return;
        }

        var highlight = events.Select(e => tree.BucketOf(e.TimeUs)).ToHashSet();

        node.DisplayBars = node.ActivityCounts
                               .Select((count, bucket) => new ActivityBar(
                                   count * tree.ActivityHeight / tree.ActivityBusiest,
                                   highlight.Contains(bucket) ? HistogramHighlight : HistogramGrey))
                               .ToList();
    }

    // Every node on the paths from the leaves up to the root.
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

    // The deepest node that is an ancestor of every leaf — where the group's paths converge.
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

    // Selects the node, walking up to the nearest shown one (the target may be hidden infrastructure), and returns it.
    private CallStackNode? SelectNode(CallStackNode? target)
    {
        for (var node = target; node is { IsRoot: false }; node = node.Parent)
        {
            if (_nodes.TryGetValue(node, out var treeNode))
            {
                Tree.SelectedNode = treeNode;

                return node;
            }
        }

        return null;
    }

    // The in-scope events: an operator scopes to every event anchored to it, a read group to its child events,
    // otherwise it is the single selected event.
    private IEnumerable<EngineEvent> ScopeEvents(EngineEvent selected)
    {
        if (selected is ExecutionOperatorEvent { PlanNodeIdentifier: { } id })
        {
            return (_viewModel?.Events ?? []).Where(e => e.PlanNodeIdentifier == id);
        }

        if (selected is NonCachedReadEventGroup group)
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

    // The nodes the tree roots at: the whole (post-infrastructure) top when no start symbol is chosen, otherwise the
    // topmost occurrences of the chosen frame (e.g. CSQLSource::Execute) so the SQLOS preamble above it is cut.
    private IEnumerable<CallStackNode> StartRoots()
    {
        var roots = _viewModel?.CallStackRoots ?? [];

        // When scoped to a selection, root at the real top so the selection's path always shows — the Start crop is
        // only for the unscoped overview, and cropping to a symbol the selection sits above would blank the tree.
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

    // The tree nodes to show for a call stack node's subtree: itself (with its visible children) when shown, or its
    // promoted visible descendants when hidden. Nodes are hidden if infrastructure (Extended Events, Tracing…) or,
    // when scoped, off the selection's paths. Children are ordered by first-seen so a start/finish sequence reads in
    // order; when scoped, everything is expanded so the selected node is revealed.
    private IEnumerable<TreeViewNode> BuildVisible(CallStackNode node)
    {
        var children = node.ChildNodes
                           .OrderBy(child => child.Order)
                           .SelectMany(BuildVisible)
                           .ToList();

        var outOfScope = _visible is not null && !_visible.Contains(node);

        var infrastructureHidden = node.IsInfrastructure && !_revealInfrastructure;

        if (outOfScope || infrastructureHidden)
        {
            foreach (var child in children)
            {
                yield return child;
            }

            yield break;
        }

        // IsExpanded is set later, once the node is attached to the tree — WinUI's TreeView can fail to realise a
        // subtree whose IsExpanded was set while the node was still detached, leaving the scoped view blank.
        var treeNode = new TreeViewNode { Content = node };

        _nodes[node] = treeNode;

        foreach (var child in children)
        {
            treeNode.Children.Add(child);
        }

        yield return treeNode;
    }
}
