using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Interfaces.Events;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class QueryCallStackTabView : UserControl
{
    private readonly Dictionary<CallStackNode, TreeViewNode> _nodes = new();

    private HashSet<CallStackNode>? _visible;

    private bool _revealInfrastructure;

    // "Focus": show the selection's own call tree on its own — an operator cut at the frame where it starts executing
    // and again where it hands off, an event cut at the barrier its work begins at. Nothing selected shows the plan's
    // operator hierarchy. Off is the whole query's stacks merged into one tree.
    //
    // Default on, matching FocusToggle.IsChecked in the XAML: the merged tree is every stack the query captured at once,
    // which is the view you narrow down FROM once you know what you are after, not the one to open on.
    private bool _focus = true;

    // Set while the tree itself is driving the selection, so the resulting change does not rebuild the tree.
    private bool _selectingFromTree;

    // Where the selection has been, so following the operator links is reversible. Every selection lands here, not only
    // the ones made in this view: arriving from the timeline and stepping back to where you were is the same movement.
    private readonly List<EngineEvent> _history = [];

    private int _historyIndex = -1;

    // Set while a Back/Forward is driving the selection, so replaying the past does not rewrite it.
    private bool _navigatingHistory;

    private string _search = string.Empty;

    // The operator rows built when scoped, so only they are auto-expanded (their call frames stay collapsed).
    private readonly List<TreeViewNode> _operatorNodes = new();

    // The operator each of the current segment's exit frames hands off to, rebuilt per operator as it is projected.
    private Dictionary<CallStackNode, ExecutionOperatorEvent> _nextOperator = new();

    // The plan's operators as a tree, rebuilt with the scoped tree.
    private OperatorHierarchy _hierarchy = OperatorHierarchy.Build([]);

    private CallStackNode? _histogramNode;

    private const string HistogramGrey = "#606060";
    private const string HistogramHighlight = "#4CA3E0";

    private TreeViewNode? _contextNode;

    private QueryViewModel? _viewModel;

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public QueryCallStackTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();
    }

    private void OnFocusChanged(object sender, RoutedEventArgs e)
    {
        _focus = FocusToggle.IsChecked == true;

        ApplyFocus(_viewModel?.SelectedEvent);
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => GoTo(_historyIndex - 1);

    private void OnForwardClick(object sender, RoutedEventArgs e) => GoTo(_historyIndex + 1);

    private void GoTo(int index)
    {
        if (_viewModel is null || index < 0 || index >= _history.Count)
        {
            return;
        }

        _historyIndex = index;

        _navigatingHistory = true;

        try
        {
            _viewModel.SelectedEvent = _history[index];
        }
        finally
        {
            _navigatingHistory = false;
        }

        UpdateHistoryButtons();
    }

    /// <summary>
    /// Records a selection as a place that can be returned to
    /// </summary>
    /// <remarks>
    /// Moving after stepping back drops whatever was ahead — the forward entries were a path from where you were, not
    /// from where you have just gone, and keeping them would offer a route that no longer connects.
    /// </remarks>
    private void RecordHistory(EngineEvent? selected)
    {
        if (selected is null || _navigatingHistory)
        {
            return;
        }

        if (_historyIndex >= 0 && ReferenceEquals(_history[_historyIndex], selected))
        {
            return;
        }

        _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

        _history.Add(selected);

        _historyIndex = _history.Count - 1;

        UpdateHistoryButtons();
    }

    private void ClearHistory()
    {
        _history.Clear();

        _historyIndex = -1;

        UpdateHistoryButtons();
    }

    private void UpdateHistoryButtons()
    {
        BackButton.IsEnabled = _historyIndex > 0;

        ForwardButton.IsEnabled = _historyIndex >= 0 && _historyIndex < _history.Count - 1;
    }

    /// <summary>
    /// Shows what the tree below is a segment of, and the way back up to the operator that drove it
    /// </summary>
    /// <remarks>
    /// Both rows are wrapped in a TreeViewNode because the header borrows the tree's templates, which bind to one.
    /// </remarks>
    private void ShowFocusHeader(ExecutionOperatorEvent? parent, object current)
    {
        // A plan root has nowhere above it, so the link goes rather than showing an arrow that leads nowhere.
        var hasParent = parent is not null;

        HeaderParent.Content = hasParent ? new TreeViewNode { Content = new OperatorLink(parent!, Back: true) } : null;

        HeaderParent.Visibility = hasParent ? Visibility.Visible : Visibility.Collapsed;

        HeaderCurrent.Content = new TreeViewNode { Content = current };

        FocusHeader.Visibility = Visibility.Visible;
    }

    private void HideFocusHeader()
    {
        HeaderParent.Content = null;

        HeaderCurrent.Content = null;

        FocusHeader.Visibility = Visibility.Collapsed;
    }

    // Following the header's parent link, like following one in the tree: a request to go there, so it rebuilds around
    // the parent rather than leaving the current segment on screen with the selection moved out from under it.
    private void OnHeaderParentTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_viewModel is null || HeaderParent.Content is not TreeViewNode { Content: OperatorLink link })
        {
            return;
        }

        _viewModel.SelectedEvent = link.Operator;
    }

    private void OnNodeRightTapped(object sender, RightTappedRoutedEventArgs e) =>
        _contextNode = (sender as FrameworkElement)?.DataContext as TreeViewNode;

    private void OnExpandAllClick(object sender, RoutedEventArgs e) => SetExpanded(_contextNode, expanded: true);

    private void OnCollapseAllClick(object sender, RoutedEventArgs e) => SetExpanded(_contextNode, expanded: false);

    // Copies the right-tapped node's subtree as the indented text dump CallStackTree.Render produces.
    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (_contextNode?.Content is not CallStackNode node)
        {
            return;
        }

        var package = new DataPackage();

        package.SetText(CallStackTree.Render(node));

        Clipboard.SetContent(package);
    }

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

        ApplyFocus(_viewModel?.SelectedEvent);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A new query's events are new objects, so nowhere the history points at exists any more.
        if (e.PropertyName == nameof(QueryViewModel.CallStack))
        {
            ClearHistory();

            ApplyFocus(_viewModel?.SelectedEvent);
        }
        else if (e.PropertyName == nameof(QueryViewModel.SelectedEvent) && !_selectingFromTree)
        {
            RecordHistory(_viewModel?.SelectedEvent);

            ApplyFocus(_viewModel?.SelectedEvent);
        }
    }

    private void ApplyFocus(EngineEvent? selected)
    {
        if (_focus)
        {
            // An operator selects its segment; anything else selects its own work. Scoping a read to the operator that
            // issued it answers a question that was not asked — the read was clicked, not the seek.
            if (selected is not null and not ExecutionOperatorEvent)
            {
                BuildEventTree(selected);
            }
            else
            {
                BuildPlanTree(SelectedNodeId(selected));
            }

            return;
        }

        var events = selected is null ? [] : ScopeEvents(selected).ToList();

        var leaves = events.Where(e => e.CallStack is not null).Select(e => e.CallStack!).Distinct().ToList();

        _visible = leaves.Count == 0 ? null : VisibleFrom(leaves);

        _revealInfrastructure = false;

        BuildTree();

        if (_visible is not null && _nodes.Count == 0)
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

    // Clicking a frame selects the event it came from, so the grid, details and timeline all answer "what is this frame
    // doing here?" — the question the tree cannot answer on its own, and the one every diagnosis of the scoping has
    // needed. A frame with no events of its own reports the earliest beneath it: that is still the work it led to.
    private void OnFrameInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (_viewModel is null || args.InvokedItem is not TreeViewNode invoked)
        {
            return;
        }

        var selected = invoked.Content switch
        {
            OperatorLink link => link.Operator,
            OperatorRow row => row.Operator,
            CallStackNode frame => EarliestEvent(frame),
            _ => null,
        };

        if (selected is null)
        {
            return;
        }

        // A hand-off link is a request to GO somewhere, so it must rebuild — that is the whole of what it does. Every
        // other row is just reporting what it already shows, and rebuilding under the click would throw away the
        // expansion the user opened to get there.
        var navigating = invoked.Content is OperatorLink;

        _selectingFromTree = !navigating;

        try
        {
            _viewModel.SelectedEvent = selected;
        }
        finally
        {
            _selectingFromTree = false;
        }
    }

    private static EngineEvent? EarliestEvent(CallStackNode node)
    {
        EngineEvent? earliest = null;

        var pending = new Stack<CallStackNode>();

        pending.Push(node);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var engineEvent in current.Events)
            {
                if (earliest is null || engineEvent.SequenceId < earliest.SequenceId)
                {
                    earliest = engineEvent;
                }
            }

            foreach (var child in current.ChildNodes)
            {
                pending.Push(child);
            }
        }

        return earliest;
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

        // Any consolidated group (a read, a lock group) scopes to the raw events it owns, merging their stacks into one
        // tree — the group itself carries no call stack, its members do.
        if (selected is IEventGroup group)
        {
            return group.Events;
        }

        return [selected];
    }

    private void BuildTree()
    {
        ClearTree();

        // Merged: the tree is the whole query, which is not a segment of anything and so heads nothing.
        HideFocusHeader();

        foreach (var root in _viewModel?.CallStackRoots ?? [])
        {
            foreach (var treeNode in BuildVisible(root))
            {
                Tree.RootNodes.Add(treeNode);
            }
        }

        ExpandForSearch();
    }

    // A filtered tree left collapsed hides the very rows the search kept, so a search opens what it found. Attached
    // first: WinUI can drop a subtree expanded while detached.
    private void ExpandForSearch()
    {
        if (_search.Length == 0)
        {
            return;
        }

        foreach (var root in Tree.RootNodes)
        {
            SetExpanded(root, expanded: true);
        }
    }

    // Drop the selection BEFORE the nodes it points into: TreeView holds SelectedNode itself, so clearing RootNodes
    // while it still references a node from the previous tree leaves the control unable to show the new one — which is
    // why the first query renders (nothing selected yet) and every one after it does not.
    private void ClearTree()
    {
        Tree.SelectedNode = null;

        _nodes.Clear();

        Tree.RootNodes.Clear();
    }


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

        // Dropped outright rather than promoting its children, unlike the filters above: those hide a row that is in the
        // way, this one has already established there is nothing under it worth showing.
        if (Filtered(node, children.Count))
        {
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

    // The plan node a selection scopes the tree to: an operator names itself, and any other event names the operator it
    // was matched to — so selecting a read in the grid or timeline isolates the operator that issued it.
    private static PlanNodeIdentifier? SelectedNodeId(EngineEvent? selected) => selected?.PlanNodeIdentifier;

    // Scoped: root the tree at the plan's operators. Each operator row carries the call tree of its own events, then its
    // child operators; an operator whose whole subtree captured no call stacks is dropped.
    //
    // With a selection, only that operator is shown — the point of scoping is to see one node's stack in isolation, so
    // the rest of the plan is not built. With nothing selected there is nothing to isolate, so the whole plan is.
    private void BuildPlanTree(PlanNodeIdentifier? selectedNode)
    {
        ClearTree();

        // The whole plan is on screen, so nothing is isolated for the header to name; an isolated operator puts it back.
        HideFocusHeader();

        _operatorNodes.Clear();

        if (_histogramNode is not null)
        {
            _histogramNode.DisplayBars = [];
            _histogramNode = null;
        }

        _hierarchy = OperatorHierarchy.Build(_viewModel?.Events ?? []);

        if (_hierarchy.Operators.Count == 0)
        {
            return;
        }

        if (selectedNode is not null
            && _hierarchy.Operators.FirstOrDefault(o => o.PlanNodeIdentifier == selectedNode) is { } selectedOperator)
        {
            BuildIsolatedOperator(selectedOperator);

            return;
        }

        foreach (var root in _hierarchy.Roots)
        {
            if (BuildOperatorNode(root) is { } node)
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

        ExpandForSearch();
    }

    /// <summary>
    /// One event's own work: its stack cut at the nearest barrier above it, with the operator's frames subtracted
    /// </summary>
    /// <remarks>
    /// The event, not its kind — two reads through the same GetPageWithKey stay apart, because the scope is this
    /// instance and the barrier only says where to stop climbing. A group merges: scoping it to what it owns puts every
    /// member's stack in one tree, which for a read group is its latches and waits together.
    /// </remarks>
    private void BuildEventTree(EngineEvent selected)
    {
        ClearTree();

        _operatorNodes.Clear();

        SetHistogram(null, []);

        if (_viewModel?.CallStack is not { } tree)
        {
            return;
        }

        _hierarchy = OperatorHierarchy.Build(_viewModel.Events ?? []);

        // An event hands off to nothing: it is the bottom of the plan.
        _nextOperator = new Dictionary<CallStackNode, ExecutionOperatorEvent>();

        // The way back up. Without it an event reached from the timeline is a dead end — there is no history to step
        // back through and the plan around it is gone.
        var op = selected.PlanNodeIdentifier is { } id
            ? _hierarchy.Operators.FirstOrDefault(o => o.PlanNodeIdentifier == id)
            : null;

        ShowFocusHeader(op, new EventRow(selected));

        var scope = selected.SelfAndOwned().ToHashSet(ReferenceEqualityComparer.Instance);

        // Cut at the barrier; failing that at the operator's own entry, so the stack is still bounded by something
        // rather than running back to the thread start. A barrier list will never cover every path.
        var projected = tree.Project(include: scope.Contains, cutAt: frame => frame.IsAccessBarrier);

        if (!projected.Root.ChildNodes.Any() && op is { EntryFrames.Count: > 0 })
        {
            projected = tree.Project(include: scope.Contains, cutAt: op.EntryFrames.Contains);
        }

        var nodes = ProjectedCallNodes(projected, revealInfrastructure: false);

        foreach (var node in nodes.Count > 0 ? nodes : ProjectedCallNodes(projected, revealInfrastructure: true))
        {
            Tree.RootNodes.Add(node);
        }

        // Attached first: WinUI can drop a subtree expanded while detached.
        foreach (var node in Tree.RootNodes)
        {
            SetExpanded(node, expanded: true);
        }
    }

    // One operator on its own, with its call tree expanded: the isolated stack for the selected node. No child operators
    // — their frames are a different node's work, which is exactly what isolating it means to exclude.
    //
    // The operator heads the view from the header rather than as a row the frames hang off, so the tree is only the
    // frames. The way back out lives there too: isolation is exactly where the plan around the operator is gone, so
    // without it the only route to the caller is the history, and there is none when the operator came from the timeline.
    private void BuildIsolatedOperator(ExecutionOperatorEvent op)
    {
        ShowFocusHeader(_hierarchy.Parent(op), new OperatorRow(op, Unsegmented: !Segmented(op)));

        foreach (var callNode in BuildOperatorCallTree(op))
        {
            Tree.RootNodes.Add(callNode);
        }

        // Attached first: WinUI can drop a subtree expanded while detached.
        foreach (var node in Tree.RootNodes)
        {
            SetExpanded(node, expanded: true);
        }
    }

    // A tree node for an operator: its own call tree followed by its child operators, or null when neither it nor any
    // descendant captured a call stack (so empty operators do not clutter the plan).
    private TreeViewNode? BuildOperatorNode(ExecutionOperatorEvent op)
    {
        var callNodes = BuildOperatorCallTree(op);

        var childOperatorNodes = _hierarchy.Children(op)
            .OrderBy(child => child.PlanNodeIdentifier!.NodeId)
            .Select(BuildOperatorNode)
            .OfType<TreeViewNode>()
            .ToList();

        // An operator the search names survives even with nothing under it: the row is itself the answer.
        var searched = _search.Length > 0 && Matches(op);

        if (callNodes.Count == 0 && childOperatorNodes.Count == 0 && !searched)
        {
            return null;
        }

        var operatorNode = new TreeViewNode { Content = new OperatorRow(op, Unsegmented: !Segmented(op)) };

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

    // An operator's call tree (infrastructure hidden), or an empty list when it has no frames of its own. Falls back to
    // revealing infrastructure if hiding it would leave the operator's paths empty.
    private List<TreeViewNode> BuildOperatorCallTree(ExecutionOperatorEvent op)
    {
        if (_viewModel?.CallStack is not { } tree)
        {
            return [];
        }

        // No entry frame means no segment. Borrowing the enclosing operator's bounds instead would render ITS segment a
        // second time under this name — SELECT and the Compute Scalar beneath it showing the same frames — which reads
        // as the operator having done that work. Empty is what was actually found, and the row still holds its place in
        // the plan, so SELECT -> Compute Scalar -> Stream Aggregate stays intact with the middle link simply carrying
        // nothing.
        if (!Segmented(op))
        {
            return [];
        }

        var scope = _hierarchy.ScopeOf(op, _viewModel?.Events ?? []);

        if (scope.Count == 0)
        {
            return [];
        }

        // A projection rather than a scope set over the shared tree: a shared node holds every event that reached that
        // function, so its event count and category would be the whole query's, not this operator's. The projected
        // nodes carry only the events in scope, which is the point of scoping to it.
        //
        // Cut top and bottom: ExitFrames drops the operators nested inside this one, and a barrier drops the storage
        // work below it — a read's descent is that read's own detail, reachable by selecting it, and inlining it here
        // buries the operator under the same hundred frames repeated for every page it touched.
        //
        // Which operator each exit frame hands off to, so the segment can name what it stopped for rather than just
        // ending. Keyed on the frame because that is what Project records having cut. A barrier hands off to no
        // operator, so it is absent here and simply ends the branch.
        _nextOperator = _hierarchy.Descendants(op)
            .SelectMany(descendant => descendant.EntryFrames.Select(frame => (Frame: frame, Operator: descendant)))
            .GroupBy(link => link.Frame)
            .ToDictionary(link => link.Key, link => link.First().Operator);

        var projected = tree.Project(include: scope.Contains,
                                     cutAt: op.EntryFrames.Contains,
                                     stopBelow: frame => op.ExitFrames.Contains(frame) || frame.IsAccessBarrier);

        var nodes = ProjectedCallNodes(projected, revealInfrastructure: false);

        return nodes.Count > 0 ? nodes : ProjectedCallNodes(projected, revealInfrastructure: true);
    }

    // Whether this operator has frames of its own. When it does not, OperatorRow.Unsegmented says so and the row renders
    // empty rather than claiming somebody else's.
    private static bool Segmented(ExecutionOperatorEvent op) => op.EntryFrames.Count > 0;

    private List<TreeViewNode> ProjectedCallNodes(CallStackTree projected, bool revealInfrastructure)
    {
        var nodes = new List<TreeViewNode>();

        foreach (var root in projected.Root.ChildNodes.OrderBy(child => child.Order))
        {
            nodes.AddRange(BuildProjectedCall(root, revealInfrastructure));
        }

        return nodes;
    }


    // Renders a projected node's subtree, hiding infrastructure and promoting its visible children. No scope set: the
    // projection already contains only the operator's frames.
    private IEnumerable<TreeViewNode> BuildProjectedCall(CallStackNode node, bool revealInfrastructure)
    {
        var children = node.ChildNodes
                           .OrderBy(child => child.Order)
                           .SelectMany(child => BuildProjectedCall(child, revealInfrastructure))
                           .ToList();

        // Where the segment stopped, name what it stopped for: a link per operator the work continues in, so the plan
        // is still walkable from inside the stack instead of the call trailing off into nothing. Searched like any other
        // row — and counted as one below, so a frame whose only answer to the search is where it led still shows.
        foreach (var next in node.CutBelow
                                 .Select(frame => _nextOperator.GetValueOrDefault(frame))
                                 .OfType<ExecutionOperatorEvent>()
                                 .Where(Matches)
                                 .DistinctBy(next => next.PlanNodeIdentifier)
                                 .OrderBy(next => next.PlanNodeIdentifier!.NodeId))
        {
            children.Add(new TreeViewNode { Content = new OperatorLink(next) });
        }

        if (node.IsInfrastructure && !revealInfrastructure)
        {
            foreach (var child in children)
            {
                yield return child;
            }

            yield break;
        }

        if (Filtered(node, children.Count))
        {
            yield break;
        }

        var treeNode = new TreeViewNode { Content = node };

        foreach (var child in children)
        {
            treeNode.Children.Add(child);
        }

        yield return treeNode;
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _search = sender.Text?.Trim() ?? string.Empty;

        ApplyFocus(_viewModel?.SelectedEvent);
    }

    /// <summary>
    /// Whether a frame answers the search, across everything it shows
    /// </summary>
    /// <remarks>
    /// Every field the row displays, because there is no telling which one the reader has in mind — a class, a category,
    /// an operator badge and a module all look like plausible things to type.
    /// </remarks>
    private bool Matches(CallStackNode node)
        => _search.Length == 0
           || Contains(node.Symbol)
           || Contains(node.Category)
           || Contains(node.Operator)
           || Contains(node.Frame?.Module);

    private bool Matches(ExecutionOperatorEvent op)
        => _search.Length == 0 || Contains(op.OperatorDescription) || Contains(op.TargetLabel) || Contains(op.Name);

    private bool Contains(string? value)
        => value is not null && value.Contains(_search, StringComparison.OrdinalIgnoreCase);

    // A row survives a search by matching it, or by being on the way to something that does — a hit is unreadable
    // without the calls that led to it, so the ancestors come too.
    private bool Filtered(CallStackNode node, int survivingChildren)
        => _search.Length > 0 && survivingChildren == 0 && !Matches(node);
}
