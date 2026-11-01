using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using Windows.Foundation;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryReferenceGraphViewModel : ObservableObject
{
    private const string TitleSuffix = " Graph";

    private const string ObjectGraphSuffix = " Object Graph";

    private const string ThisName = "this";

    private const string FrameType = "Buffer Pool Frame";

    private const string BufferType = "BUF";

    private const string TypeSeparator = " / ";

    private const int GraphLimit = 300;

    private const int HotDepth = 6;

    private const int SearchLimit = 20;

    private const int LoadInterval = 50;

    private const double NodeWidth = 230;

    private const double NodeHeight = 48;

    private const double ColumnGap = 132;

    private const double TrunkGap = 22;

    private const double UpLinkTrunkGap = 10;

    private const double DownLinkTrunkGap = 16;

    private const double FanInGap = 12;

    private const double LinkPortOffset = 14;

    private const double LaneHeight = 30;

    private const double CompactLaneHeight = 18;

    private const double LaneBelow = 14;

    private const double RowGap = 10;

    private const double GraphMargin = 16;

    [ObservableProperty]
    private MemoryReferenceGraph? _graph;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    public MemoryReferenceGraphViewModel(MemoryHistoryViewModel history, int block, TracePosition position, ITraceNavigator navigator)
        : this(history, block, null, position, navigator, history.GetObjectName(block) + TitleSuffix)
    {
    }

    public MemoryReferenceGraphViewModel(MemoryHistoryViewModel history, TraceStartCall start, ITraceNavigator navigator)
        : this(history, -1, start, start.Call.FromPosition, navigator, start.Node.Symbol + ObjectGraphSuffix)
    {
    }

    private MemoryReferenceGraphViewModel(MemoryHistoryViewModel history,
                                          int block,
                                          TraceStartCall? start,
                                          TracePosition position,
                                          ITraceNavigator navigator,
                                          string title)
    {
        History = history;

        Block = block;

        StartCall = start;

        OpenedAt = position;

        Navigator = navigator;

        Expanded = [string.Empty];

        Loader = new PlayheadLoader<GraphLoad?>(TimeSpan.FromMilliseconds(LoadInterval), ApplyGraph, b => IsLoading = b);

        RootTitle = title;

        Title = title;

        Refresh();
    }

    public bool IsReferenceHot { get; private set; }

    public bool IsDataHot { get; private set; }

    private bool IsHot => IsReferenceHot || IsDataHot;

    private MemoryHistoryViewModel History { get; }

    private int Block { get; }

    private TraceStartCall? StartCall { get; }

    private string RootTitle { get; }

    private int? SelectedBlock { get; set; }

    private ITraceNavigator Navigator { get; }

    private TracePosition OpenedAt { get; }

    private TracePosition? Playhead { get; set; }

    private TracePosition Position => Playhead ?? OpenedAt;

    private HashSet<string> Expanded { get; }

    private HashSet<string> ExpandedTrees { get; } = [];

    private HashSet<string> Collapsed { get; } = [];

    private HashSet<string> ReferrersExpanded { get; } = [];

    private PlayheadLoader<GraphLoad?> Loader { get; }

    private MemoryHistoryGraph? Shown { get; set; }

    private List<(MemoryReferenceGraphEdge Edge, MemoryHistoryGraphEdge Pointer)> MovingEdges { get; set; } = [];

    private ConcurrentDictionary<int, string> TypeNames { get; } = new();

    private bool IsHighlighting { get; set; }

    public void SetPlayhead(TracePosition? position)
    {
        Playhead = position;

        UpdateValues();
    }

    public void Close()
    {
        ClearHighlight();

        Loader.Cancel();

        Shown = null;

        Graph = null;
    }

    public void RefreshScope() => Refresh();

    public void SetHot(bool isReferenceHot, bool isDataHot)
    {
        IsReferenceHot = isReferenceHot;

        IsDataHot = isDataHot;

        Refresh();
    }

    public List<MemoryReferenceGraphSearchResult> Search(string text)
    {
        var terms = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (terms.Length == 0 || Shown is not { } model || Graph is not { } graph)
        {
            return [];
        }

        var position = Position;

        var results = new List<MemoryReferenceGraphSearchResult>();

        var found = new HashSet<(int Node, string Address)>();

        for (var index = 0; index < model.Nodes.Count; index++)
        {
            var targets = model.Nodes[index].Targets;

            if (targets.Length == 0)
            {
                Add(index, graph.Nodes[index].Types, string.Empty, string.Empty);
            }

            foreach (var target in targets)
            {
                var address = target.Page is null ? $"0x{History.Store.Blocks[target.Block].Address:X}" : string.Empty;

                Add(index, GetTypeName(model.Nodes[index], target), GetValueText(target, position), address);
            }
        }

        return
        [
            .. results.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                      .ThenBy(r => r.Address, StringComparer.Ordinal)
                      .Take(SearchLimit)
        ];

        void Add(int index, string name, string address, string compact)
        {
            if (terms.All(t => name.Contains(t, StringComparison.OrdinalIgnoreCase)
                               || address.Contains(t, StringComparison.OrdinalIgnoreCase)
                               || compact.Contains(t, StringComparison.OrdinalIgnoreCase))
                && found.Add((index, address)))
            {
                results.Add(new MemoryReferenceGraphSearchResult(graph.Nodes[index], name, address));
            }
        }
    }

    private void Toggle(string path)
    {
        if (IsExpanded(path))
        {
            Expanded.Remove(path);

            if (IsExpanded(path))
            {
                Collapsed.Add(path);
            }
        }
        else
        {
            Collapsed.Remove(path);

            if (!IsExpanded(path))
            {
                Expanded.Add(path);
            }
        }

        Refresh();
    }

    private void ExpandAll(string path)
    {
        ExpandedTrees.Add(path);

        Collapsed.RemoveWhere(p => IsWithin(p, path));

        Refresh();
    }

    private void CollapseAll(string path)
    {
        ExpandedTrees.RemoveWhere(p => IsWithin(p, path));

        Expanded.RemoveWhere(p => IsWithin(p, path));

        Collapsed.RemoveWhere(p => IsWithin(p, path));

        ReferrersExpanded.RemoveWhere(p => IsWithin(p, path)
                                           || p.StartsWith(path + MemoryHistoryGraph.ReferrerSeparator, StringComparison.Ordinal));

        if (IsExpanded(path))
        {
            Collapsed.Add(path);
        }

        Refresh();
    }

    private bool IsExpanded(string path) => IsExpanded(path, Expanded, ExpandedTrees, Collapsed);

    private static bool IsExpanded(string path, HashSet<string> expanded, HashSet<string> trees, HashSet<string> collapsed)
        => !collapsed.Contains(path) && (expanded.Contains(path) || trees.Any(t => IsWithin(path, t)));

    private static bool IsWithin(string path, string tree)
        => path == tree || path.StartsWith(tree + MemoryHistoryGraph.PathSeparator, StringComparison.Ordinal);

    private void ToggleReferrers(string path)
    {
        if (!ReferrersExpanded.Remove(path))
        {
            ReferrersExpanded.Add(path);
        }

        Refresh();
    }

    private void Select(MemoryHistoryGraphNode node)
    {
        if (GetBlock(node, Position) is not { } block)
        {
            return;
        }

        SetRoot(block, History.GetObjectName(block) + TitleSuffix);
    }

    private void Reset() => SetRoot(null, RootTitle);

    private void SetRoot(int? block, string title)
    {
        SelectedBlock = block;

        Title = title;

        Expanded.Clear();

        Expanded.Add(string.Empty);

        ExpandedTrees.Clear();

        Collapsed.Clear();

        ReferrersExpanded.Clear();

        Refresh();
    }

    private void Refresh()
    {
        var store = History.Store;

        var pool = History.BufferPool;

        var block = SelectedBlock ?? Block;

        var start = SelectedBlock is null ? StartCall : null;

        var from = History.WindowStart;

        var to = History.WindowEnd;

        var shutdown = History.ShutdownStart;

        Func<int, MemoryHistoryArray?> getArray = History.GetArray;

        HashSet<string> expanded = [.. Expanded];

        HashSet<string> trees = [.. ExpandedTrees];

        HashSet<string> collapsed = [.. Collapsed];

        HashSet<string> referrers = [.. ReferrersExpanded];

        var isReferenceHot = IsReferenceHot;

        var isDataHot = IsDataHot;

        var isHot = IsHot;

        Func<string, bool> isExpanded = p => isHot || IsExpanded(p, expanded, trees, collapsed);

        var context = new GraphContext(isHot,
                                       SelectedBlock is not null,
                                       isExpanded,
                                       referrers.Contains,
                                       GetWindow(),
                                       History.Scale,
                                       shutdown);

        Loader.Request(() => Load(isHot ? Build(_ => true, HotDepth).GetHot(isReferenceHot, isDataHot) : Build(isExpanded, int.MaxValue)));

        GraphLoad Load(MemoryHistoryGraph model)
        {
            List<(MemoryReferenceGraphEdge Edge, MemoryHistoryGraphEdge Pointer)> moving = [];

            return new GraphLoad(model, GetGraph(model, context, moving), moving);
        }

        MemoryHistoryGraph Build(Func<string, bool> isExpanded, int depth)
            => start is null
                ? MemoryHistoryGraph.Build(store,
                                           pool,
                                           block,
                                           from,
                                           to,
                                           shutdown,
                                           isExpanded,
                                           referrers.Contains,
                                           depth,
                                           GraphLimit,
                                           getArray)
                : MemoryHistoryGraph.BuildFromCall(store,
                                                   pool,
                                                   start.Call.IntegerSlots,
                                                   start.Call.FromPosition,
                                                   from,
                                                   to,
                                                   shutdown,
                                                   isExpanded,
                                                   referrers.Contains,
                                                   depth,
                                                   GraphLimit,
                                                   getArray);
    }

    private void ApplyGraph(GraphLoad? load)
    {
        ClearHighlight();

        Shown = load?.Model;

        MovingEdges = load?.MovingEdges ?? [];

        Graph = load?.Graph;

        Status = load is null ? string.Empty : GetStatusText(load.Graph);

        UpdateValues();
    }

    private static string GetStatusText(MemoryReferenceGraph graph)
    {
        var count = graph.Nodes.Count;

        if (graph.IsTruncated)
        {
            return $"Limited to {count:N0} Objects";
        }

        return count == 1 ? "1 Object" : $"{count:N0} Objects";
    }

    private void UpdateValues()
    {
        if (Shown is not { } graph || Graph is not { } shown)
        {
            return;
        }

        var position = Position;

        var (start, end) = GetWindow();

        Progress = end > start ? Math.Clamp((History.Scale.GetValue(position) - start) / (end - start), 0, 1) : 0;

        for (var index = 0; index < graph.Nodes.Count; index++)
        {
            var node = shown.Nodes[index];

            var target = graph.Nodes[index].GetTarget(position);

            node.Label = target is { } found ? GetTypeName(graph.Nodes[index], found) : node.Types;

            node.Detail = target is { } value ? GetValueText(value, position) : string.Empty;

            node.CanOpen = target is { } openable && CanOpen(openable);
        }

        foreach (var (edge, pointer) in MovingEdges)
        {
            edge.Label = GetPointerLabel(pointer, graph.GetTargetOffset(pointer, position));
        }
    }

    private MemoryReferenceGraph GetGraph(MemoryHistoryGraph graph,
                                          GraphContext context,
                                          List<(MemoryReferenceGraphEdge Edge, MemoryHistoryGraphEdge Pointer)> moving)
    {
        var count = graph.Nodes.Count;

        var children = new List<int>[count];

        var referrers = new List<int>[count];

        for (var index = 0; index < count; index++)
        {
            children[index] = [];

            referrers[index] = [];
        }

        for (var index = 1; index < count; index++)
        {
            var node = graph.Nodes[index];

            (node.IsReferrer ? referrers : children)[node.Parent].Add(index);
        }

        List<List<MemoryHistoryGraphEdge>> groups =
        [
            .. graph.Edges.Where(e => e.From != e.To).GroupBy(e => (e.From, e.To)).Select(g => g.ToList())
        ];

        var spans = new int[count];

        Array.Fill(spans, 1);

        foreach (var pointers in groups.Where(p => IsFan(graph, p)))
        {
            spans[pointers[0].To] = GetFanRows(pointers);
        }

        var rows = new int[count];

        var rowCount = count == 0 ? 0 : Place(children, referrers, rows, spans, 0, 0, true);

        var first = count == 0 ? 0 : graph.Nodes.Min(n => n.Depth);

        var nodes = new List<MemoryReferenceGraphNode>(count);

        for (var index = 0; index < count; index++)
        {
            nodes.Add(CreateNode(graph.Nodes[index],
                                 GraphMargin + (graph.Nodes[index].Depth - first) * (NodeWidth + ColumnGap),
                                 GraphMargin + rows[index] * (NodeHeight + RowGap),
                                 index == 0,
                                 context));
        }

        var edges = new List<MemoryReferenceGraphEdge>();

        foreach (var pointers in groups)
        {
            var from = nodes[pointers[0].From];

            var to = nodes[pointers[0].To];

            if (IsFan(graph, pointers))
            {
                var laneHeight = GetLaneHeight(pointers);

                for (var index = 0; index < pointers.Count; index++)
                {
                    AddEdge(pointers[index], widest => CreateLane(from, to, pointers[index], (index + 1) * laneHeight, widest));
                }

                continue;
            }

            var isReferrer = graph.Nodes[pointers[0].From].IsReferrer;

            var isSideEntry = isReferrer && pointers[0].To != 0 && !graph.Nodes[pointers[0].To].IsReferrer;

            var isTree = graph.IsTreeEdge(pointers[0]);

            if (pointers.Count == 1)
            {
                AddEdge(pointers[0], widest => CreateEdge(from, to, pointers, isTree, isReferrer, isSideEntry, widest));
            }
            else
            {
                edges.Add(CreateEdge(from, to, pointers, isTree, isReferrer, isSideEntry, null));
            }
        }

        var columns = count == 0 ? 0 : graph.Nodes.Max(n => n.Depth) - first + 1;

        void AddEdge(MemoryHistoryGraphEdge pointer, Func<int?, MemoryReferenceGraphEdge> create)
        {
            var widest = pointer.TargetOffset is null ? graph.GetMaximumTargetOffset(pointer) : null;

            var edge = create(widest);

            edges.Add(edge);

            if (widest is not null)
            {
                moving.Add((edge, pointer));
            }
        }

        return new MemoryReferenceGraph(nodes,
                                        edges,
                                        GraphMargin * 2 + columns * (NodeWidth + ColumnGap) - ColumnGap,
                                        GraphMargin * 2 + rowCount * (NodeHeight + RowGap) - RowGap,
                                        NodeWidth,
                                        NodeHeight,
                                        graph.IsTruncated,
                                        context.IsSelected ? Reset : null);
    }

    private static int Place(List<int>[] children,
                             List<int>[] referrers,
                             int[] rows,
                             int[] spans,
                             int node,
                             int row,
                             bool isLeftClear)
    {
        rows[node] = row;

        var next = row + 1;

        var isFirst = true;

        foreach (var child in children[node])
        {
            var childRow = isFirst ? row : next;

            next = Math.Max(Place(children, referrers, rows, spans, child, childRow, false), childRow + spans[child]);

            isFirst = false;
        }

        var referrerRow = isLeftClear ? row : row + 1;

        foreach (var referrer in referrers[node])
        {
            referrerRow = Place(children, referrers, rows, spans, referrer, referrerRow, true);
        }

        return Math.Max(next, referrerRow);
    }

    private static bool IsFan(MemoryHistoryGraph graph, List<MemoryHistoryGraphEdge> pointers)
        => pointers.Count > 1 && graph.IsTreeEdge(pointers[0]) && !graph.Nodes[pointers[0].From].IsReferrer;

    private static double GetLaneHeight(List<MemoryHistoryGraphEdge> pointers)
        => pointers.Any(p => p.ChangeCount > 0) ? LaneHeight : CompactLaneHeight;

    private static int GetFanRows(List<MemoryHistoryGraphEdge> pointers)
    {
        var height = NodeHeight / 2 + pointers.Count * GetLaneHeight(pointers) + LaneBelow;

        return Math.Max(1, (int)Math.Ceiling(height / (NodeHeight + RowGap)));
    }

    private MemoryReferenceGraphNode CreateNode(MemoryHistoryGraphNode node,
                                                double x,
                                                double y,
                                                bool isRoot,
                                                GraphContext context)
    {
        var types = node.Targets.Length == 0 && StartCall is { } start
            ? start.Node.Symbol
            : string.Join(TypeSeparator, node.Targets.Select(t => GetTypeName(node, t)).Distinct());

        var canToggle = !context.IsHot && !node.IsData && !node.IsReferrer && node.HasPointers;

        var canToggleReferrers = !context.IsHot && !node.IsData && node.HasReferrers;

        var canSelect = !isRoot && (node.Targets.Any(t => t.Block >= 0) || node.Object is { Block: >= 0 });

        var path = node.Path;

        return new MemoryReferenceGraphNode(types,
                                            x,
                                            y,
                                            isRoot,
                                            node.IsData,
                                            context.IsExpanded(path),
                                            context.IsReferrersExpanded(path),
                                            GetActiveSpans(node, context),
                                            GetMarks(node, context),
                                            GetDataChangesText(node.DataChangeCount),
                                            () => Open(node),
                                            canToggle ? () => Toggle(path) : null,
                                            canToggleReferrers ? () => ToggleReferrers(path) : null,
                                            canSelect ? () => Select(node) : null,
                                            canToggle ? () => ExpandAll(path) : null,
                                            canToggle || canToggleReferrers ? () => CollapseAll(path) : null,
                                            isHovered => Hover(node, isHovered));
    }

    private void Hover(MemoryHistoryGraphNode node, bool isHovered)
    {
        var block = isHovered ? GetBlock(node, Position) : null;

        IsHighlighting = block is not null;

        Navigator.HighlightMemory(block);
    }

    private static int? GetBlock(MemoryHistoryGraphNode node, TracePosition position)
    {
        if (node.GetTarget(position) is { } current)
        {
            return current.Block >= 0 ? current.Block : null;
        }

        int? block = null;

        foreach (var target in node.Targets)
        {
            if (target.Block < 0)
            {
                continue;
            }

            if (block is not null && target.From > position)
            {
                break;
            }

            block = target.Block;
        }

        return block ?? (node.Object is { Block: >= 0 } found ? found.Block : null);
    }

    private void ClearHighlight()
    {
        if (!IsHighlighting)
        {
            return;
        }

        IsHighlighting = false;

        Navigator.HighlightMemory(null);
    }

    private MemoryReferenceGraphSpan[] GetActiveSpans(MemoryHistoryGraphNode node, GraphContext context)
    {
        var (start, end) = context.Window;

        if (end <= start)
        {
            return [];
        }

        var scale = context.Scale;

        var spans = new List<MemoryReferenceGraphSpan>();

        var blocks = History.Store.Blocks;

        var lifetimes = node.Targets.Select(t => t.Block >= 0
                                                ? (From: blocks[t.Block].FromPosition,
                                                   To: blocks[t.Block].ToPosition,
                                                   Colour: History.GetAllocationColour(t.Block))
                                                : (t.From, t.To, Colour: null));

        foreach (var lifetime in lifetimes.Distinct().OrderBy(l => l.From))
        {
            var from = Math.Max(0, (scale.GetValue(lifetime.From) - start) / (end - start));

            var to = lifetime.To.IsKnown ? Math.Min(1, (scale.GetValue(lifetime.To) - start) / (end - start)) : 1;

            if (to <= from)
            {
                continue;
            }

            if (spans.Count > 0 && from <= spans[^1].End && spans[^1].Colour == lifetime.Colour)
            {
                spans[^1] = spans[^1] with { End = Math.Max(spans[^1].End, to) };
            }
            else
            {
                spans.Add(new MemoryReferenceGraphSpan(from, to, lifetime.Colour));
            }
        }

        return [.. spans];
    }

    private MemoryReferenceGraphMark[] GetMarks(MemoryHistoryGraphNode node, GraphContext context)
    {
        var (start, end) = context.Window;

        if (end <= start)
        {
            return [];
        }

        var scale = context.Scale;

        return
        [
            .. node.Marks
                   .Where(m => m.Position < context.Shutdown)
                   .OrderBy(m => m.IsReference)
                   .Select(m => new MemoryReferenceGraphMark((scale.GetValue(m.Position) - start) / (end - start),
                                                             History.GetCategoryColour(m.CallNodeIndex)))
                   .Where(m => m.At > 0 && m.At < 1)
        ];
    }

    private (double Start, double End) GetWindow()
    {
        var scale = History.Scale;

        var end = History.WindowEnd;

        return (scale.GetValue(History.WindowStart), end.IsKnown ? scale.GetValue(end) : 0);
    }

    private void Open(MemoryHistoryGraphNode node)
    {
        var position = Position;

        if (node.GetTarget(position) is not { } target)
        {
            return;
        }

        if (target.Page is { } page)
        {
            if (History.IsQueryDatabase(page))
            {
                Navigator.OpenBufferPage(page);
            }

            return;
        }

        Navigator.OpenMemory(target.Block, target.Value, position);
    }

    private bool CanOpen(MemoryHistoryGraphTarget target) => target.Page is not { } page || History.IsQueryDatabase(page);

    private string GetTypeName(MemoryHistoryGraphNode node, MemoryHistoryGraphTarget target)
        => node.Element is { } element ? $"{GetTypeName(target)}[{element.Index}]" : GetTypeName(target);

    private string GetTypeName(MemoryHistoryGraphTarget target)
    {
        if (target.Page is { } page)
        {
            return page.IsBuffer ? BufferType : FrameType;
        }

        return TypeNames.GetOrAdd(target.Block, History.GetTypeName);
    }

    private string GetValueText(MemoryHistoryGraphTarget target, TracePosition position)
    {
        if (target.Page is { } page)
        {
            return History.GetBufferPageName(page);
        }

        var address = History.Store.Blocks[target.Block].Address;

        return History.IsBuffer(target.Block) && History.GetBufferPage(address, position) is { } buffer
            ? $"{History.GetBufferPageName(buffer)}  0x{address:X16}"
            : $"0x{address:X16}";
    }

    private MemoryReferenceGraphEdge CreateEdge(MemoryReferenceGraphNode from,
                                                       MemoryReferenceGraphNode to,
                                                       List<MemoryHistoryGraphEdge> pointers,
                                                       bool isTree,
                                                       bool isReferrer,
                                                       bool isSideEntry,
                                                       int? widest)
    {
        var isUp = to.Y < from.Y;

        var portOffset = isTree ? 0 : isUp ? -LinkPortOffset : LinkPortOffset;

        var start = new Point(from.X + NodeWidth, from.Y + NodeHeight / 2 + portOffset);

        var isForward = to.X > from.X;

        var end = new Point(isForward ? to.X : to.X + NodeWidth,
                            to.Y + NodeHeight / 2 - portOffset + (isSideEntry ? LinkPortOffset : 0));

        var trunk = isReferrer ? end.X - TrunkGap : start.X + (isTree ? TrunkGap : isUp ? UpLinkTrunkGap : DownLinkTrunkGap);

        Point[] points = IsDetour(from, to)
            ? GetDetour(from, to, start, isUp)
            : [start, new Point(trunk, start.Y), new Point(trunk, end.Y), end];

        var labelAt = isReferrer || !isTree
            ? start
            : new Point(isForward ? Math.Max(start.X + TrunkGap, Math.Min(trunk, end.X)) : end.X, end.Y);

        var isLabelBelow = !isTree && isUp;

        var label = pointers.Count == 1 ? GetPointerLabel(pointers[0]) : $"{GetFieldText(pointers[0])} ({pointers.Count})";

        var widestLabel = widest is { } offset ? GetPointerLabel(pointers[0], offset) : label;

        var changes = pointers.Sum(p => p.ChangeCount);

        var detail = string.Join(Environment.NewLine, pointers.Select(p => $"{GetPointerLabel(p)}, {GetChangesText(p.ChangeCount)}"));

        return new MemoryReferenceGraphEdge(from,
                                            to,
                                            points,
                                            labelAt,
                                            label,
                                            widestLabel,
                                            changes > 0 && isTree ? GetChangesText(changes) : string.Empty,
                                            detail,
                                            changes,
                                            !isTree,
                                            isLabelBelow);
    }

    private MemoryReferenceGraphEdge CreateLane(MemoryReferenceGraphNode from,
                                                       MemoryReferenceGraphNode to,
                                                       MemoryHistoryGraphEdge pointer,
                                                       double drop,
                                                       int? widest)
    {
        var start = new Point(from.X + NodeWidth, from.Y + NodeHeight / 2);

        var end = new Point(to.X, to.Y + NodeHeight / 2);

        var fanOut = start.X + TrunkGap;

        var fanIn = end.X - FanInGap;

        var lane = end.Y + drop;

        Point[] points = [start, new(fanOut, start.Y), new(fanOut, lane), new(fanIn, lane), new(fanIn, end.Y), end];

        var label = GetPointerLabel(pointer);

        return new MemoryReferenceGraphEdge(from,
                                            to,
                                            points,
                                            new Point(fanOut, lane),
                                            label,
                                            widest is { } offset ? GetPointerLabel(pointer, offset) : label,
                                            pointer.ChangeCount > 0 ? GetChangesText(pointer.ChangeCount) : string.Empty,
                                            $"{GetPointerLabel(pointer)}, {GetChangesText(pointer.ChangeCount)}",
                                            pointer.ChangeCount,
                                            false,
                                            false);
    }

    private static bool IsDetour(MemoryReferenceGraphNode from, MemoryReferenceGraphNode to)
    {
        var columns = Math.Round((to.X - from.X) / (NodeWidth + ColumnGap));

        return columns is < 0 or > 1;
    }

    private static Point[] GetDetour(MemoryReferenceGraphNode from, MemoryReferenceGraphNode to, Point start, bool isUp)
    {
        var isBelow = from.Y >= to.Y;

        var gap = isBelow ? to.Y + NodeHeight + RowGap / 2 : to.Y - RowGap / 2;

        var exit = start.X + (isUp ? UpLinkTrunkGap : DownLinkTrunkGap);

        var isForward = to.X > from.X;

        var end = new Point(isForward ? to.X : to.X + NodeWidth,
                            to.Y + NodeHeight / 2 + (isBelow ? LinkPortOffset : -LinkPortOffset));

        var entry = isForward ? end.X - FanInGap : end.X + (isUp ? UpLinkTrunkGap : DownLinkTrunkGap);

        return [start, new Point(exit, start.Y), new Point(exit, gap), new Point(entry, gap), new Point(entry, end.Y), end];
    }

    private string GetPointerLabel(MemoryHistoryGraphEdge pointer)
        => GetPointerLabel(pointer, pointer.TargetOffset is > 0 and var offset ? offset : null);

    private string GetPointerLabel(MemoryHistoryGraphEdge pointer, int? targetOffset)
        => targetOffset is { } offset ? $"{GetFieldText(pointer)} → {GetOffsetText(offset)}" : GetFieldText(pointer);

    private string GetFieldText(MemoryHistoryGraphEdge pointer)
        => StartCall is { } start && pointer.From == 0 ? GetArgumentName(start, pointer.Offset) : GetOffsetText(pointer.Offset);

    private static string GetArgumentName(TraceStartCall start, int slot)
        => slot switch
        {
            0 when start.Node.Frame?.Resolved?.ClassName is { Length: > 0 } => ThisName,
            < ArgumentLayout.RegisterCount => ArgumentLayout.Registers[slot],
            _ => ArgumentLayout.GetStackLocation(slot)
        };

    private static string GetOffsetText(int offset) => $"+0x{offset:X}";

    private static string GetChangesText(int changes) => changes == 1 ? "1 Change" : $"{changes:N0} Changes";

    private static string GetDataChangesText(int changes)
        => changes switch
        {
            0 => string.Empty,
            1 => "1 Data Change",
            _ => $"{changes:N0} Data Changes"
        };

    private sealed record GraphLoad(MemoryHistoryGraph Model,
                                    MemoryReferenceGraph Graph,
                                    List<(MemoryReferenceGraphEdge Edge, MemoryHistoryGraphEdge Pointer)> MovingEdges);

    private sealed record GraphContext(bool IsHot,
                                       bool IsSelected,
                                       Func<string, bool> IsExpanded,
                                       Func<string, bool> IsReferrersExpanded,
                                       (double Start, double End) Window,
                                       TraceScale Scale,
                                       TracePosition Shutdown);
}
