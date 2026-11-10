using InternalsViewer.Query.FullTrace.Iterators;
using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Models.Query.Trace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Calls;

public sealed partial class CallTreeViewModel
{
    private Dictionary<CallTreeNode, CallTreeRow> Rows { get; } = new();

    private List<CallTreeRow> Roots { get; set; } = [];

    private List<CallTreeRow> Expanded { get; } = [];

    private List<CallTreeRow> OperatorRows { get; } = [];

    private bool ExpandRoots { get; set; }

    private CallTreeRow? Selected { get; set; }

    private object? FocusCurrent { get; set; }

    private OperatorLink? FocusParent { get; set; }

    private object? FilterCurrent { get; set; }

    private CallTreeNode? RevealTarget { get; set; }

    private HashSet<CallTreeNode>? Revealed { get; set; }

    private HashSet<CallTreeRow> SearchHits { get; } = [];

    private HashSet<CallTreeRow> SearchMatches { get; } = [];

    private string? SelectedPath { get; set; }

    private string? FilterPath { get; set; }

    private string Search { get; set; } = string.Empty;

    private bool IsFiltering => Search.Length > 0;

    private LocatorSearch? SearchedLocator { get; set; }

    private Dictionary<CallTreeNode, ExecutionOperatorEvent> NextOperator { get; set; } = new();

    private OperatorHierarchy Hierarchy { get; set; } = OperatorHierarchy.Build([]);

    private OperatorFrameMap Frames => Query.OperatorFrames;

    private IReadOnlySet<string> HiddenCategories => Settings.CallTreeHiddenCategorySet;

    private IReadOnlySet<string> PrunedCategories => Settings.CallTreePrunedCategorySet;

    public CallTreeRow? GetRow(CallTreeNode? target)
    {
        for (var node = target; node is { IsRoot: false }; node = node.Parent)
        {
            if (Rows.TryGetValue(node, out var row))
            {
                return row;
            }
        }

        return null;
    }

    private void Publish()
    {
        SearchMatchCount = IsFiltering ? SearchMatches.Count : null;

        Layout = new CallTreeLayout([.. Roots], [.. Expanded], ExpandRoots, Selected, FocusCurrent, FocusParent, FilterCurrent);
    }

    private void ApplyFocus(EngineEvent? selected, bool keepSelection = false)
    {
        NavigatedNode = null;

        if (IsFocused)
        {
            BuildPlanTree(SelectedNodeId(selected));

            ApplyFilter();

            if (keepSelection)
            {
                KeepSelection();
            }

            ApplyActivityBands();

            Publish();

            return;
        }

        BuildTree();

        ApplyFilter();

        if (!keepSelection || !KeepSelection())
        {
            SelectNode(null);
        }

        ApplyActivityBands();

        Publish();
    }

    private bool KeepSelection()
    {
        if (SelectedPath is not { } path || FindRow(Roots, path) is not { } row)
        {
            return false;
        }

        Selected = row;

        return true;
    }

    private void ApplyActivityBands()
    {
        foreach (var row in CallTreeRow.Walk(Roots))
        {
            if (row.Content is CallTreeNode node)
            {
                node.Activity = IsActivityVisible
                    ? new ActivityBand([.. row.Children.Select(c => c.Content).OfType<CallTreeNode>()])
                    : null;
            }
        }
    }

    private void SelectNode(CallTreeNode? target)
    {
        Selected = GetRow(target);

        SelectedPath = Selected?.Content is CallTreeNode node ? GetPath(node) : null;
    }

    private void BuildTree()
    {
        ClearTree();

        HideFocusHeader();

        foreach (var row in BuildVisible(Query.CallTreeRoots, Reveal.None))
        {
            Roots.Add(row);
        }

        ExpandForSearch();
    }

    private void ExpandForSearch()
    {
        if (!IsFiltering)
        {
            return;
        }

        ExpandRoots = false;

        var pending = new Queue<CallTreeRow>(Roots);

        var shown = 0;

        while (pending.Count > 0 && shown < SearchExpandBudget)
        {
            var row = pending.Dequeue();

            if (!row.Children.Any(SearchHits.Contains))
            {
                continue;
            }

            Expanded.Add(row);

            shown += row.Children.Count;

            foreach (var child in row.Children)
            {
                pending.Enqueue(child);
            }
        }
    }

    private void ClearTree()
    {
        Selected = null;

        Rows.Clear();

        Roots = [];

        Expanded.Clear();

        ExpandRoots = false;

        SearchHits.Clear();

        SearchMatches.Clear();
    }

    private void RevealPath(CallTreeNode target)
    {
        var path = target.GetAncestors().ToList();

        RevealTarget = path.Any(IsConcealed) ? target : null;

        Revealed = RevealTarget is null ? null : [.. path];
    }

    private bool IsOutsideCrop(CallTreeNode node) => Query.CroppedRoots is { } cropped && !node.GetAncestors().Any(cropped.Contains);

    private void ClearReveal()
    {
        RevealTarget = null;

        Revealed = null;
    }

    private bool IsRevealed(CallTreeNode node)
        => RevealTarget is { } target
           && (node.MergedFrom.Count > 0 ? node.MergedFrom : [node]).Any(n => Revealed!.Contains(n) || n.GetAncestors().Contains(target));

    /// <summary>
    /// Shows what the tree below is a segment of, and the way back up to the operator that drove it
    /// </summary>
    /// <remarks>
    /// Both rows are wrapped in a TreeViewNode because the header borrows the tree's templates, which bind to one.
    /// </remarks>
    private void ShowFocusHeader(ExecutionOperatorEvent? parent, object current)
    {
        // A plan root has nowhere above it, so the link goes rather than showing an arrow that leads nowhere.
        FocusParent = parent is not null ? new OperatorLink(parent, Back: true) : null;

        FocusCurrent = current;
    }

    private void HideFocusHeader()
    {
        FocusParent = null;

        FocusCurrent = null;
    }

    private List<CallTreeRow> BuildVisible(IEnumerable<CallTreeNode> nodes, Reveal reveal, bool underMatch = false)
        => [.. nodes.SelectMany(n => GetVisibleNodes(n, reveal))
                    .GroupBy(n => n.Key)
                    .Select(g => BuildVisibleRow([.. g], reveal, underMatch))
                    .OfType<CallTreeRow>()];

    private IEnumerable<CallTreeNode> GetVisibleNodes(CallTreeNode node, Reveal reveal)
    {
        if (Revealed?.Contains(node) == true)
        {
            return [node];
        }

        if (reveal == Reveal.Path || CallExclusions.IsIn(PrunedCategories, node))
        {
            return [];
        }

        return IsHidden(node)
            ? node.ChildNodes.OrderBy(child => child.Order).SelectMany(child => GetVisibleNodes(child, reveal))
            : [node];
    }

    private CallTreeRow? BuildVisibleRow(IReadOnlyList<CallTreeNode> group, Reveal reveal, bool underMatch)
    {
        var node = group.Count == 1 ? group[0] : CallTreeNode.Merge(group);

        var matched = IsSearchMatch(node);

        var revealed = Revealed is { } path && group.Any(path.Contains);

        var children = BuildVisible(group.SelectMany(n => n.ChildNodes.OrderBy(child => child.Order)),
                                    GetChildReveal(group, reveal),
                                    underMatch || matched);

        // Dropped outright rather than promoting its children, unlike the filters above: those hide a row that is in the
        // way, this one has already established there is nothing under it worth showing.
        if (!revealed && Filtered(children.Count, underMatch || matched))
        {
            return null;
        }

        var row = new CallTreeRow(node) { IsRevealed = reveal != Reveal.None || (revealed && group.All(IsConcealed)) };

        foreach (var member in group)
        {
            Rows[member] = row;
        }

        foreach (var child in children)
        {
            row.Add(child);
        }

        AddSearchHit(row, matched);

        return row;
    }

    private Reveal GetChildReveal(IReadOnlyList<CallTreeNode> group, Reveal reveal)
    {
        if (reveal == Reveal.Under)
        {
            return Reveal.Under;
        }

        var cut = reveal == Reveal.Path || group.Any(n => Revealed?.Contains(n) == true && CallExclusions.IsIn(PrunedCategories, n));

        if (!cut)
        {
            return Reveal.None;
        }

        return RevealTarget is { } target && group.Contains(target) ? Reveal.Under : Reveal.Path;
    }

    private bool IsHidden(CallTreeNode node) => node.IsInfrastructure || CallExclusions.IsIn(HiddenCategories, node);

    private bool IsConcealed(CallTreeNode node) => IsHidden(node) || CallExclusions.IsIn(PrunedCategories, node);

    private bool IsSearchMatch(CallTreeNode node) => Search.Length > 0 && Matches(node);

    private void AddSearchHit(CallTreeRow row, bool isHit)
    {
        if (IsFiltering && (isHit || row.Children.Any(SearchHits.Contains)))
        {
            SearchHits.Add(row);
        }

        if (IsFiltering && isHit)
        {
            SearchMatches.Add(row);
        }
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

        OperatorRows.Clear();

        Hierarchy = OperatorHierarchy.Build(Query.Events ?? []);

        if (Hierarchy.Operators.Count == 0)
        {
            return;
        }

        if (selectedNode is not null
            && Hierarchy.Operators.FirstOrDefault(o => o.PlanNodeIdentifier == selectedNode) is { } selectedOperator)
        {
            BuildIsolatedOperator(selectedOperator);

            return;
        }

        foreach (var root in Hierarchy.Roots)
        {
            if (BuildOperatorNode(root) is { } row)
            {
                Roots.Add(row);
            }
        }

        // Expand the operator hierarchy once the nodes are attached (WinUI can drop a subtree expanded while detached);
        // the call frames under each operator stay collapsed so the plan stays readable.
        Expanded.AddRange(OperatorRows);

        ExpandForSearch();
    }

    // One operator on its own, with its call tree expanded: the isolated stack for the selected node. No child operators
    // — their frames are a different node's work, which is exactly what isolating it means to exclude.
    //
    // The operator heads the view from the header rather than as a row the frames hang off, so the tree is only the
    // frames. The way back out lives there too: isolation is exactly where the plan around the operator is gone, so
    // without it the only route to the caller is the history, and there is none when the operator came from the timeline.
    private void BuildIsolatedOperator(ExecutionOperatorEvent op)
    {
        ShowFocusHeader(Hierarchy.Parent(op), new OperatorRow(op, Unsegmented: !Segmented(op)));

        foreach (var row in BuildOperatorCallTree(op))
        {
            Roots.Add(row);
        }

        // Attached first: WinUI can drop a subtree expanded while detached.
        ExpandRoots = true;

        ExpandForSearch();
    }

    // A tree node for an operator: its own call tree followed by its child operators, or null when neither it nor any
    // descendant captured a call stack (so empty operators do not clutter the plan).
    private CallTreeRow? BuildOperatorNode(ExecutionOperatorEvent op)
    {
        var callNodes = BuildOperatorCallTree(op);

        var childOperatorNodes = Hierarchy.Children(op)
            .OrderBy(child => child.PlanNodeIdentifier!.NodeId)
            .Select(BuildOperatorNode)
            .OfType<CallTreeRow>()
            .ToList();

        // An operator the search names survives even with nothing under it: the row is itself the answer.
        var searched = Search.Length > 0 && Matches(op);

        if (callNodes.Count == 0 && childOperatorNodes.Count == 0 && !searched)
        {
            return null;
        }

        var operatorNode = new CallTreeRow(new OperatorRow(op, Unsegmented: !Segmented(op)));

        foreach (var callNode in callNodes)
        {
            operatorNode.Add(callNode);
        }

        foreach (var childOperatorNode in childOperatorNodes)
        {
            operatorNode.Add(childOperatorNode);
        }

        OperatorRows.Add(operatorNode);

        AddSearchHit(operatorNode, searched);

        return operatorNode;
    }

    // An operator's call tree (infrastructure hidden), or an empty list when it has no frames of its own. Falls back to
    // revealing infrastructure if hiding it would leave the operator's paths empty.
    private List<CallTreeRow> BuildOperatorCallTree(ExecutionOperatorEvent op)
    {
        if (Query.CallTree is not { } tree)
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
        NextOperator = Hierarchy.Descendants(op)
            .SelectMany(descendant => Frames.GetEntryFrames(descendant).Select(frame => (Frame: frame, Operator: descendant)))
            .GroupBy(link => link.Frame)
            .ToDictionary(link => link.Key, link => link.First().Operator);

        var projected = tree.Project(cutAt: Frames.GetEntryFrames(op).Contains, stopBelow: Frames.GetExitFrames(op).Contains);

        var nodes = ProjectedCallNodes(projected, revealInfrastructure: false);

        return nodes.Count > 0 ? nodes : ProjectedCallNodes(projected, revealInfrastructure: true);
    }

    // Whether this operator has frames of its own. When it does not, OperatorRow.Unsegmented says so and the row renders
    // empty rather than claiming somebody else's.
    private bool Segmented(ExecutionOperatorEvent op) => Frames.GetEntryFrames(op).Count > 0;

    private List<CallTreeRow> ProjectedCallNodes(CallTree projected, bool revealInfrastructure)
        => BuildProjected(projected.Root.ChildNodes.OrderBy(child => child.Order), revealInfrastructure, underMatch: false);

    // Renders a projected node's subtree, hiding infrastructure and promoting its visible children. No scope set: the
    // projection already contains only the operator's frames.
    private List<CallTreeRow> BuildProjected(IEnumerable<CallTreeNode> nodes, bool revealInfrastructure, bool underMatch)
    {
        var promotedCuts = new List<CallTreeNode>();

        var visible = nodes.SelectMany(n => GetVisibleProjectedNodes(n, revealInfrastructure, promotedCuts)).ToList();

        List<CallTreeRow> rows =
            [.. visible.GroupBy(n => n.Key).Select(g => BuildProjectedRow([.. g], revealInfrastructure, underMatch)).OfType<CallTreeRow>()];

        rows.AddRange(OperatorLinks(promotedCuts, underMatch));

        return rows;
    }

    private IEnumerable<CallTreeNode> GetVisibleProjectedNodes(CallTreeNode node, bool revealInfrastructure, List<CallTreeNode> promotedCuts)
    {
        if (CallExclusions.IsIn(PrunedCategories, node))
        {
            return [];
        }

        if ((node.IsInfrastructure && !revealInfrastructure) || CallExclusions.IsIn(HiddenCategories, node))
        {
            promotedCuts.AddRange(node.CutBelow);

            return node.ChildNodes
                       .OrderBy(child => child.Order)
                       .SelectMany(child => GetVisibleProjectedNodes(child, revealInfrastructure, promotedCuts));
        }

        return [node];
    }

    private CallTreeRow? BuildProjectedRow(IReadOnlyList<CallTreeNode> group, bool revealInfrastructure, bool underMatch)
    {
        var node = group.Count == 1 ? group[0] : CallTreeNode.Merge(group);

        var matched = IsSearchMatch(node);

        var children = BuildProjected(group.SelectMany(n => n.ChildNodes.OrderBy(child => child.Order)),
                                      revealInfrastructure,
                                      underMatch || matched);

        children.AddRange(OperatorLinks(group.SelectMany(n => n.CutBelow), underMatch || matched));

        if (Filtered(children.Count, underMatch || matched))
        {
            return null;
        }

        var row = new CallTreeRow(node);

        foreach (var child in children)
        {
            row.Add(child);
        }

        AddSearchHit(row, matched);

        return row;
    }

    private IEnumerable<CallTreeRow> OperatorLinks(IEnumerable<CallTreeNode> cuts, bool underMatch)
    {
        // Where the segment stopped, name what it stopped for: a link per operator the work continues in, so the plan
        // is still walkable from inside the stack instead of the call trailing off into nothing. Searched like any other
        // row — and counted as one below, so a frame whose only answer to the search is where it led still shows.
        foreach (var next in cuts.Select(frame => NextOperator.GetValueOrDefault(frame))
                                 .OfType<ExecutionOperatorEvent>()
                                 .Where(next => underMatch || Matches(next))
                                 .DistinctBy(next => next.PlanNodeIdentifier)
                                 .OrderBy(next => next.PlanNodeIdentifier!.NodeId))
        {
            var row = new CallTreeRow(new OperatorLink(next));

            AddSearchHit(row, Matches(next));

            yield return row;
        }
    }

    private void ApplyFilter()
    {
        if (FilterPath is null)
        {
            FilterCurrent = null;

            return;
        }

        if (FindRow(Roots, FilterPath) is not { } row)
        {
            FilterPath = null;

            FilterCurrent = null;

            return;
        }

        var subtree = CallTreeRow.Walk([row]).ToHashSet();

        foreach (var (node, mapped) in Rows.ToList())
        {
            if (!subtree.Contains(mapped))
            {
                Rows.Remove(node);
            }
        }

        Selected = null;

        Expanded.RemoveAll(r => !subtree.Contains(r));

        row.Detach();

        Roots = [row];

        ExpandRoots = !IsFiltering;

        FilterCurrent = row.Content;
    }

    private static CallTreeRow? FindRow(IEnumerable<CallTreeRow> rows, string path)
        => CallTreeRow.Walk(rows).FirstOrDefault(r => r.Content is CallTreeNode node && GetPath(node) == path);

    private static string GetPath(CallTreeNode node) => string.Join('\n', node.GetAncestors().Select(a => a.Key));

    private static string GetRowPath(CallTreeRow row)
    {
        var keys = new List<string>();

        for (var current = row; current is not null; current = current.Parent)
        {
            keys.Add(GetRowKey(current));
        }

        return string.Join('\n', keys);
    }

    private static string GetRowKey(CallTreeRow row) => row.Content switch
    {
        CallTreeNode node => node.Key,
        OperatorRow operatorRow => $"Operator {operatorRow.Operator.PlanNodeIdentifier}",
        OperatorLink link => $"Link {link.Operator.PlanNodeIdentifier}",
        _ => string.Empty
    };

    private Dictionary<string, CallTreeRow> GetRowsByPath()
    {
        var rows = new Dictionary<string, CallTreeRow>(StringComparer.Ordinal);

        foreach (var row in CallTreeRow.Walk(Roots))
        {
            rows.TryAdd(GetRowPath(row), row);
        }

        return rows;
    }

    /// <summary>
    /// Whether a frame answers the search, across everything it shows
    /// </summary>
    /// <remarks>
    /// Every field the row displays, because there is no telling which one the reader has in mind — a class, a category,
    /// an operator badge and a module all look like plausible things to type.
    /// </remarks>
    private bool Matches(CallTreeNode node)
    {
        if (SearchedLocator is { } locator)
        {
            return node.Locators?.Contains(locator) == true;
        }

        return Search.Length == 0
               || Contains(node.Symbol)
               || Contains(node.Category)
               || Contains(node.Operator)
               || Contains(node.Frame?.Module.Name);
    }

    private bool Matches(ExecutionOperatorEvent op)
        => Search.Length == 0
           || SearchedLocator is null
           && (Contains(op.OperatorDescription) || Contains(op.TargetLabel) || Contains(op.Name));

    private bool Contains(string? value)
        => value is not null && value.Contains(Search, StringComparison.OrdinalIgnoreCase);

    // A row survives a search by matching it, or by being on the way to something that does — a hit is unreadable
    // without the calls that led to it, so the ancestors come too.
    private bool Filtered(int survivingChildren, bool searched) => survivingChildren == 0 && Search.Length > 0 && !searched;

    private enum Reveal
    {
        None,
        Path,
        Under,
    }
}
