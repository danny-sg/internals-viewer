using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Interfaces.Events;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed partial class CallTreeViewModel
{
    private Dictionary<CallStackNode, CallTreeRow> Rows { get; } = new();

    private List<CallTreeRow> Roots { get; set; } = [];

    private List<CallTreeRow> Expanded { get; } = [];

    private List<CallTreeRow> OperatorRows { get; } = [];

    private Dictionary<CallStackNode, EventVisibility> HiddenOnly { get; } = new();

    private bool ExpandRoots { get; set; }

    private CallTreeRow? Selected { get; set; }

    private object? FocusCurrent { get; set; }

    private OperatorLink? FocusParent { get; set; }

    private object? FilterCurrent { get; set; }

    private CallStackNode? RevealTarget { get; set; }

    private HashSet<CallStackNode>? Revealed { get; set; }

    private HashSet<CallTreeRow> SearchHits { get; } = [];

    private string? SelectedPath { get; set; }

    private string? FilterPath { get; set; }

    private HashSet<int> HighlightBuckets { get; set; } = [];

    private string Search { get; set; } = string.Empty;

    private LocatorSearch? SearchedLocator { get; set; }

    private Dictionary<CallStackNode, ExecutionOperatorEvent> NextOperator { get; set; } = new();

    private OperatorHierarchy Hierarchy { get; set; } = OperatorHierarchy.Build([]);

    private HashSet<string> HiddenCategories
    {
        get
        {
            var setting = Settings.CallTreeHiddenCategories;

            if (!string.Equals(setting, HiddenCategoriesSetting, StringComparison.Ordinal))
            {
                HiddenCategoriesSetting = setting;

                HiddenCategoriesCache = SettingsViewModel.SplitCategories(setting);
            }

            return HiddenCategoriesCache;
        }
    }

    private HashSet<string> PrunedCategories
    {
        get
        {
            var setting = Settings.CallTreePrunedCategories;

            if (!string.Equals(setting, PrunedCategoriesSetting, StringComparison.Ordinal))
            {
                PrunedCategoriesSetting = setting;

                PrunedCategoriesCache = SettingsViewModel.SplitCategories(setting);
            }

            return PrunedCategoriesCache;
        }
    }

    private string? HiddenCategoriesSetting { get; set; }

    private HashSet<string> HiddenCategoriesCache { get; set; } = new(StringComparer.Ordinal);

    private string? PrunedCategoriesSetting { get; set; }

    private HashSet<string> PrunedCategoriesCache { get; set; } = new(StringComparer.Ordinal);

    public CallTreeRow? GetRow(CallStackNode? target)
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
        => Layout = new CallTreeLayout([.. Roots], [.. Expanded], ExpandRoots, Selected, FocusCurrent, FocusParent, FilterCurrent);

    private void ApplyFocus(EngineEvent? selected, bool keepSelection = false)
    {
        NavigatedNode = null;

        SetHighlightBuckets(selected);

        if (IsFocused)
        {
            if (selected is not null and not ExecutionOperatorEvent)
            {
                BuildEventTree(selected);
            }
            else
            {
                BuildPlanTree(SelectedNodeId(selected));
            }

            ApplyFilter();

            if (keepSelection)
            {
                KeepSelection();
            }

            ApplyActivityBands();

            Publish();

            return;
        }

        var events = selected is null ? [] : ScopeEvents(selected).ToList();

        var leaves = events.Where(e => e.CallStack is not null).Select(e => e.CallStack!).Distinct().ToList();

        BuildTree();

        ApplyFilter();

        ExpandTo(leaves);

        if (!keepSelection || !KeepSelection())
        {
            var target = leaves.Count switch
            {
                0 => null,
                1 => leaves[0],
                _ => CommonAncestor(leaves),
            };

            SelectNode(target);
        }

        ApplyActivityBands();

        Publish();
    }

    private void ExpandTo(List<CallStackNode> leaves)
    {
        foreach (var row in leaves.Select(GetRow).OfType<CallTreeRow>().Distinct().Take(ExpandedLeaves))
        {
            if (row.Parent is { } parent)
            {
                Expanded.Add(parent);
            }
        }
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
        var pending = new Stack<CallTreeRow>(Roots);

        while (pending.Count > 0)
        {
            var row = pending.Pop();

            if (row.Content is CallStackNode node)
            {
                node.Activity = IsActivityShown
                    ? new ActivityBand(HighlightBuckets, [.. row.Children.Select(c => c.Content).OfType<CallStackNode>()])
                    : null;
            }

            foreach (var child in row.Children)
            {
                pending.Push(child);
            }
        }
    }

    private ActivitySpan? OperatorSpan(ExecutionOperatorEvent op)
    {
        var tree = Query.CallStack;

        if (tree is null || tree.ActivityFromTrace || tree.ActivityMaxUs <= tree.ActivityMinUs)
        {
            return null;
        }

        var startUs = op.TimeUs;

        var endUs = op.DurationUs > 0 ? op.TimeUs + op.DurationUs : startUs;

        if (op.DurationUs <= 0)
        {
            var events = ScopeEvents(op).ToList();

            if (events.Count == 0)
            {
                return null;
            }

            startUs = events.Min(e => e.TimeUs);
            endUs = events.Max(e => e.TimeUs);
        }

        var window = (double)(tree.ActivityMaxUs - tree.ActivityMinUs);

        var start = Math.Clamp((startUs - tree.ActivityMinUs) / window, 0, 1);

        var end = Math.Clamp((endUs - tree.ActivityMinUs) / window, start, 1);

        return new ActivitySpan(start, Math.Max(end, start + 1d / tree.ActivityBuckets));
    }

    private void SetHighlightBuckets(EngineEvent? selected)
    {
        var tree = Query.CallStack;

        if (selected is null or ExecutionOperatorEvent || tree is null || tree.ActivityFromTrace)
        {
            HighlightBuckets = [];

            return;
        }

        HighlightBuckets = ScopeEvents(selected).Select(e => tree.BucketOf(e.TimeUs)).ToHashSet();
    }

    private static CallStackNode? CommonAncestor(List<CallStackNode> leaves)
    {
        var common = AncestorsAndSelf(leaves[0]).ToHashSet();

        for (var i = 1; i < leaves.Count; i++)
        {
            common.IntersectWith(AncestorsAndSelf(leaves[i]));
        }

        return common.OrderByDescending(GetDepth).FirstOrDefault();
    }

    private static IEnumerable<CallStackNode> AncestorsAndSelf(CallStackNode node)
    {
        for (var current = node; current is { IsRoot: false }; current = current.Parent)
        {
            yield return current;
        }
    }

    private static int GetDepth(CallStackNode node)
    {
        var depth = 0;

        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            depth++;
        }

        return depth;
    }

    private void SelectNode(CallStackNode? target)
    {
        Selected = GetRow(target);

        SelectedPath = Selected?.Content is CallStackNode node ? GetPath(node) : null;
    }

    private IEnumerable<EngineEvent> ScopeEvents(EngineEvent selected)
    {
        if (selected is ExecutionOperatorEvent { PlanNodeIdentifier: { } id })
        {
            return (Query.Events ?? []).Where(e => e.PlanNodeIdentifier == id);
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

        foreach (var row in BuildVisible(Query.CallStackRoots, Reveal.None))
        {
            AddRoot(row);
        }

        if (GetRevealedRoot() is { } outside)
        {
            foreach (var row in BuildVisible([outside], Reveal.Path))
            {
                var position = Roots.FindIndex(r => r.Content is CallStackNode { Order: var order } && order > outside.Order);

                Roots.Insert(position < 0 ? Roots.Count : position, row);
            }
        }

        ExpandForSearch();
    }

    // A filtered tree left collapsed hides the very rows the search kept, so a search opens what it found. Attached
    // first: WinUI can drop a subtree expanded while detached.
    private void ExpandForSearch()
    {
        if (Search.Length == 0)
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

        HiddenOnly.Clear();

        SearchHits.Clear();
    }

    private void RevealPath(CallStackNode target)
    {
        var path = target.Ancestors().ToList();

        var outside = Query.CroppedRoots is { } cropped && !path.Any(cropped.Contains);

        RevealTarget = outside || path.Any(IsConcealed) ? target : null;

        Revealed = RevealTarget is null ? null : [.. path];
    }

    private void ClearReveal()
    {
        RevealTarget = null;

        Revealed = null;
    }

    private bool IsRevealed(CallStackNode node)
        => RevealTarget is { } target
           && (node.MergedFrom.Count > 0 ? node.MergedFrom : [node]).Any(n => Revealed!.Contains(n) || n.Ancestors().Contains(target));

    private CallStackNode? GetRevealedRoot()
    {
        if (RevealTarget is not { } target || Query.CroppedRoots is not { } cropped)
        {
            return null;
        }

        var path = target.Ancestors().ToList();

        return path.Any(cropped.Contains) ? null : path[^1];
    }

    private void AddRoot(CallTreeRow row) => Roots.Add(row);

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

    private List<CallTreeRow> BuildVisible(IEnumerable<CallStackNode> nodes, Reveal reveal, bool underMatch = false)
        => [.. nodes.SelectMany(n => Shown(n, reveal))
                    .GroupBy(n => n.Key)
                    .Select(g => BuildVisibleRow([.. g], reveal, underMatch))
                    .OfType<CallTreeRow>()];

    private IEnumerable<CallStackNode> Shown(CallStackNode node, Reveal reveal)
    {
        if (Revealed?.Contains(node) == true)
        {
            return [node];
        }

        if (reveal == Reveal.Path || IsCategoryIn(PrunedCategories, node))
        {
            return [];
        }

        return IsHidden(node)
            ? node.ChildNodes.OrderBy(child => child.Order).SelectMany(child => Shown(child, reveal))
            : [node];
    }

    private CallTreeRow? BuildVisibleRow(IReadOnlyList<CallStackNode> group, Reveal reveal, bool underMatch)
    {
        var node = group.Count == 1 ? group[0] : CallStackNode.Merge(group);

        var matched = IsSearchMatch(node);

        var revealed = Revealed is { } path && group.Any(path.Contains);

        var children = BuildVisible(group.SelectMany(n => n.ChildNodes.OrderBy(child => child.Order)),
                                    GetChildReveal(group, reveal),
                                    underMatch || matched);

        // Dropped outright rather than promoting its children, unlike the filters above: those hide a row that is in the
        // way, this one has already established there is nothing under it worth showing.
        if (!revealed && Filtered(node, children.Count, underMatch || matched))
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

    private Reveal GetChildReveal(IReadOnlyList<CallStackNode> group, Reveal reveal)
    {
        if (reveal == Reveal.Under)
        {
            return Reveal.Under;
        }

        var cut = reveal == Reveal.Path || group.Any(n => Revealed?.Contains(n) == true && IsCategoryIn(PrunedCategories, n));

        if (!cut)
        {
            return Reveal.None;
        }

        return RevealTarget is { } target && group.Contains(target) ? Reveal.Under : Reveal.Path;
    }

    private bool IsHidden(CallStackNode node) => node.IsInfrastructure || IsCategoryIn(HiddenCategories, node);

    private bool IsConcealed(CallStackNode node) => IsHidden(node) || IsCategoryIn(PrunedCategories, node);

    private bool IsSearchMatch(CallStackNode node) => Search.Length > 0 && Matches(node);

    private void AddSearchHit(CallTreeRow row, bool matched)
    {
        if (Search.Length > 0 && (matched || row.Children.Any(SearchHits.Contains)))
        {
            SearchHits.Add(row);
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
                AddRoot(row);
            }
        }

        // Expand the operator hierarchy once the nodes are attached (WinUI can drop a subtree expanded while detached);
        // the call frames under each operator stay collapsed so the plan stays readable.
        Expanded.AddRange(OperatorRows);

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

        OperatorRows.Clear();

        if (Query.CallStack is not { } tree)
        {
            return;
        }

        Hierarchy = OperatorHierarchy.Build(Query.Events ?? []);

        // An event hands off to nothing: it is the bottom of the plan.
        NextOperator = new Dictionary<CallStackNode, ExecutionOperatorEvent>();

        // The way back up. Without it an event reached from the timeline is a dead end — there is no history to step
        // back through and the plan around it is gone.
        var op = selected.PlanNodeIdentifier is { } id
            ? Hierarchy.Operators.FirstOrDefault(o => o.PlanNodeIdentifier == id)
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

        var rows = ProjectedCallNodes(projected, revealInfrastructure: false);

        foreach (var row in rows.Count > 0 ? rows : ProjectedCallNodes(projected, revealInfrastructure: true))
        {
            AddRoot(row);
        }

        // Attached first: WinUI can drop a subtree expanded while detached.
        ExpandRoots = true;

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
            AddRoot(row);
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

        var operatorNode = new CallTreeRow(new OperatorRow(op, Unsegmented: !Segmented(op), OperatorSpan(op)));

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
        if (Query.CallStack is not { } tree)
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

        var scope = Hierarchy.ScopeOf(op, Query.Events ?? []);

        if (scope.Count == 0 && !tree.ActivityFromTrace)
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

    private List<CallTreeRow> ProjectedCallNodes(CallStackTree projected, bool revealInfrastructure)
        => BuildProjected(projected.Root.ChildNodes.OrderBy(child => child.Order), revealInfrastructure, underMatch: false);

    // Renders a projected node's subtree, hiding infrastructure and promoting its visible children. No scope set: the
    // projection already contains only the operator's frames.
    private List<CallTreeRow> BuildProjected(IEnumerable<CallStackNode> nodes, bool revealInfrastructure, bool underMatch)
    {
        var promotedCuts = new List<CallStackNode>();

        var shown = nodes.SelectMany(n => ShownProjected(n, revealInfrastructure, promotedCuts)).ToList();

        List<CallTreeRow> rows =
            [.. shown.GroupBy(n => n.Key).Select(g => BuildProjectedRow([.. g], revealInfrastructure, underMatch)).OfType<CallTreeRow>()];

        rows.AddRange(OperatorLinks(promotedCuts, underMatch));

        return rows;
    }

    private IEnumerable<CallStackNode> ShownProjected(CallStackNode node, bool revealInfrastructure, List<CallStackNode> promotedCuts)
    {
        if (IsCategoryIn(PrunedCategories, node))
        {
            return [];
        }

        if ((node.IsInfrastructure && !revealInfrastructure) || IsCategoryIn(HiddenCategories, node))
        {
            promotedCuts.AddRange(node.CutBelow);

            return node.ChildNodes
                       .OrderBy(child => child.Order)
                       .SelectMany(child => ShownProjected(child, revealInfrastructure, promotedCuts));
        }

        return [node];
    }

    private CallTreeRow? BuildProjectedRow(IReadOnlyList<CallStackNode> group, bool revealInfrastructure, bool underMatch)
    {
        var node = group.Count == 1 ? group[0] : CallStackNode.Merge(group);

        var matched = IsSearchMatch(node);

        var children = BuildProjected(group.SelectMany(n => n.ChildNodes.OrderBy(child => child.Order)),
                                      revealInfrastructure,
                                      underMatch || matched);

        children.AddRange(OperatorLinks(group.SelectMany(n => n.CutBelow), underMatch || matched));

        if (Filtered(node, children.Count, underMatch || matched))
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

    private IEnumerable<CallTreeRow> OperatorLinks(IEnumerable<CallStackNode> cuts, bool underMatch)
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

        var subtree = new HashSet<CallTreeRow>();

        var pending = new Stack<CallTreeRow>();

        pending.Push(row);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            subtree.Add(current);

            foreach (var child in current.Children)
            {
                pending.Push(child);
            }
        }

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

        ExpandRoots = Search.Length == 0;

        FilterCurrent = row.Content;
    }

    private static CallTreeRow? FindRow(IEnumerable<CallTreeRow> rows, string path)
    {
        var pending = new Stack<CallTreeRow>(rows);

        while (pending.Count > 0)
        {
            var row = pending.Pop();

            if (row.Content is CallStackNode node && GetPath(node) == path)
            {
                return row;
            }

            foreach (var child in row.Children)
            {
                pending.Push(child);
            }
        }

        return null;
    }

    private static string GetPath(CallStackNode node) => string.Join('\n', node.Ancestors().Select(a => a.Key));

    private static bool IsCategoryIn(HashSet<string> categories, CallStackNode node)
        => node.Frame is not null && categories.Count > 0 && categories.Contains(node.Category);

    /// <summary>
    /// Whether a frame answers the search, across everything it shows
    /// </summary>
    /// <remarks>
    /// Every field the row displays, because there is no telling which one the reader has in mind — a class, a category,
    /// an operator badge and a module all look like plausible things to type.
    /// </remarks>
    private bool Matches(CallStackNode node)
    {
        if (SearchedLocator is { } locator)
        {
            return node.Locators?.Contains(locator) == true;
        }

        return Search.Length == 0
               || Contains(node.Symbol)
               || Contains(node.Category)
               || Contains(node.Operator)
               || Contains(node.Frame?.Module);
    }

    private bool Matches(ExecutionOperatorEvent op)
        => Search.Length == 0
           || SearchedLocator is null
           && (Contains(op.OperatorDescription) || Contains(op.TargetLabel) || Contains(op.Name));

    private bool Contains(string? value)
        => value is not null && value.Contains(Search, StringComparison.OrdinalIgnoreCase);

    // A row survives a search by matching it, or by being on the way to something that does — a hit is unreadable
    // without the calls that led to it, so the ancestors come too.
    private bool Filtered(CallStackNode node, int survivingChildren, bool searched)
        => survivingChildren == 0 && ((Search.Length > 0 && !searched) || HoldsOnlyHiddenEvents(node));

    private bool HoldsOnlyHiddenEvents(CallStackNode node) => HiddenEventState(node, Query.QueryOptions) == EventVisibility.Hidden;

    private EventVisibility HiddenEventState(CallStackNode node, QueryOptionsViewModel options)
    {
        if (HiddenOnly.TryGetValue(node, out var cached))
        {
            return cached;
        }

        var state = EventVisibility.None;

        foreach (var engineEvent in node.Events)
        {
            if (!IsHiddenKind(engineEvent, options))
            {
                state = EventVisibility.Shown;

                break;
            }

            state = EventVisibility.Hidden;
        }

        foreach (var child in node.ChildNodes)
        {
            if (state == EventVisibility.Shown)
            {
                break;
            }

            var childState = HiddenEventState(child, options);

            state = childState == EventVisibility.None ? state : childState;
        }

        HiddenOnly[node] = state;

        return state;
    }

    private static bool IsHiddenKind(EngineEvent engineEvent, QueryOptionsViewModel options) => engineEvent switch
    {
        LatchEvent => !options.ShowLatches,
        LockEvent lockEvent => !options.Includes(LockModeClassifier.Categorise(lockEvent.LockMode)),
        _ => false,
    };

    private enum EventVisibility
    {
        None,
        Hidden,
        Shown,
    }

    private enum Reveal
    {
        None,
        Path,
        Under,
    }
}
