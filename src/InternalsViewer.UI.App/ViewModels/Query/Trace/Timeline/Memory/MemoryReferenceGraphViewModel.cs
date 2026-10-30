using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using Windows.Foundation;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryReferenceGraphViewModel : ObservableObject
{
    private const string TitleSuffix = " Graph";

    private const string FrameType = "Buffer Pool Frame";

    private const string BufferType = "BUF";

    private const string TypeSeparator = " / ";

    private const int GraphLimit = 300;

    private const int HotDepth = 6;

    private const int LoadInterval = 50;

    private const double NodeWidth = 240;

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

    public MemoryReferenceGraphViewModel(MemoryHistoryViewModel history, int block, TracePosition position, ITraceNavigator navigator)
    {
        History = history;

        Block = block;

        OpenedAt = position;

        Navigator = navigator;

        Expanded = [string.Empty];

        Loader = new PlayheadLoader<MemoryHistoryGraph?>(TimeSpan.FromMilliseconds(LoadInterval), ApplyGraph);

        Title = history.GetObjectName(block) + TitleSuffix;

        Refresh();
    }

    public string Title { get; }

    public bool IsReferenceHot { get; private set; }

    public bool IsDataHot { get; private set; }

    private bool IsHot => IsReferenceHot || IsDataHot;

    private MemoryHistoryViewModel History { get; }

    private int Block { get; }

    private ITraceNavigator Navigator { get; }

    private TracePosition OpenedAt { get; }

    private TracePosition? Playhead { get; set; }

    private TracePosition Position => Playhead ?? OpenedAt;

    private HashSet<string> Expanded { get; }

    private HashSet<string> ReferrersExpanded { get; } = [];

    private PlayheadLoader<MemoryHistoryGraph?> Loader { get; }

    private MemoryHistoryGraph? Shown { get; set; }

    private Dictionary<int, string> TypeNames { get; } = [];

    public void SetPlayhead(TracePosition? position)
    {
        Playhead = position;

        UpdateValues();
    }

    public void Close()
    {
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

    private void Toggle(string path)
    {
        if (!Expanded.Remove(path))
        {
            Expanded.Add(path);
        }

        Refresh();
    }

    private void ToggleReferrers(string path)
    {
        if (!ReferrersExpanded.Remove(path))
        {
            ReferrersExpanded.Add(path);
        }

        Refresh();
    }

    private void Refresh()
    {
        var store = History.Store;

        var pool = History.BufferPool;

        var block = Block;

        var from = History.WindowStart;

        var to = History.WindowEnd;

        var shutdown = History.ShutdownStart;

        HashSet<string> expanded = [.. Expanded];

        HashSet<string> referrers = [.. ReferrersExpanded];

        var isReferenceHot = IsReferenceHot;

        var isDataHot = IsDataHot;

        if (IsHot)
        {
            Loader.Request(() => Build(_ => true, HotDepth).GetHot(isReferenceHot, isDataHot));
        }
        else
        {
            Loader.Request(() => Build(expanded.Contains, int.MaxValue));
        }

        MemoryHistoryGraph Build(Func<string, bool> isExpanded, int depth)
            => MemoryHistoryGraph.Build(store, pool, block, from, to, shutdown, isExpanded, referrers.Contains, depth, GraphLimit);
    }

    private void ApplyGraph(MemoryHistoryGraph? graph)
    {
        Shown = graph;

        Graph = graph is null ? null : GetGraph(graph);

        UpdateValues();
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

            node.Label = target is { } found ? GetTypeName(found) : node.Types;

            node.Detail = target is { } value ? GetValueText(value, position) : string.Empty;

            node.CanOpen = target is { } openable && CanOpen(openable);
        }
    }

    private MemoryReferenceGraph GetGraph(MemoryHistoryGraph graph)
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

        var rowCount = count == 0 ? 0 : Math.Max(Place(children, rows, spans, 0, 0), Place(referrers, rows, spans, 0, 0));

        var first = count == 0 ? 0 : graph.Nodes.Min(n => n.Depth);

        var window = GetWindow();

        var nodes = new List<MemoryReferenceGraphNode>(count);

        for (var index = 0; index < count; index++)
        {
            nodes.Add(CreateNode(graph.Nodes[index],
                                 GraphMargin + (graph.Nodes[index].Depth - first) * (NodeWidth + ColumnGap),
                                 GraphMargin + rows[index] * (NodeHeight + RowGap),
                                 index == 0,
                                 window));
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
                    edges.Add(CreateLane(from, to, pointers[index], (index + 1) * laneHeight));
                }

                continue;
            }

            edges.Add(CreateEdge(from, to, pointers, graph.IsTreeEdge(pointers[0]), graph.Nodes[pointers[0].From].IsReferrer));
        }

        var columns = count == 0 ? 0 : graph.Nodes.Max(n => n.Depth) - first + 1;

        return new MemoryReferenceGraph(nodes,
                                        edges,
                                        GraphMargin * 2 + columns * (NodeWidth + ColumnGap) - ColumnGap,
                                        GraphMargin * 2 + rowCount * (NodeHeight + RowGap) - RowGap,
                                        NodeWidth,
                                        NodeHeight,
                                        graph.IsTruncated);
    }

    private static int Place(List<int>[] children, int[] rows, int[] spans, int node, int row)
    {
        rows[node] = row;

        var next = row + 1;

        var isFirst = true;

        foreach (var child in children[node])
        {
            var childRow = isFirst ? row : next;

            next = Math.Max(Place(children, rows, spans, child, childRow), childRow + spans[child]);

            isFirst = false;
        }

        return Math.Max(next, row + 1);
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
                                                (double Start, double End) window)
    {
        var types = string.Join(TypeSeparator, node.Targets.Select(GetTypeName).Distinct());

        var canToggle = !IsHot && !node.IsData && !node.IsReferrer && node.HasPointers;

        var canToggleReferrers = !IsHot && node.Depth <= 0 && node.HasReferrers;

        var path = node.Path;

        return new MemoryReferenceGraphNode(types,
                                            x,
                                            y,
                                            isRoot,
                                            node.IsData,
                                            IsHot || Expanded.Contains(path),
                                            ReferrersExpanded.Contains(path),
                                            GetActiveSpans(node, window),
                                            GetMarks(node, window),
                                            GetDataChangesText(node.DataChangeCount),
                                            () => Open(node),
                                            canToggle ? () => Toggle(path) : null,
                                            canToggleReferrers ? () => ToggleReferrers(path) : null);
    }

    private (double Start, double End)[] GetActiveSpans(MemoryHistoryGraphNode node, (double Start, double End) window)
    {
        var (start, end) = window;

        if (end <= start)
        {
            return [];
        }

        var scale = History.Scale;

        var spans = new List<(double Start, double End)>();

        var blocks = History.Store.Blocks;

        var lifetimes = node.Targets.Select(t => t.Block >= 0
                                                ? (From: blocks[t.Block].FromPosition, To: blocks[t.Block].ToPosition)
                                                : (t.From, t.To));

        foreach (var lifetime in lifetimes.Distinct().OrderBy(l => l.From))
        {
            var from = Math.Max(0, (scale.GetValue(lifetime.From) - start) / (end - start));

            var to = lifetime.To.IsKnown ? Math.Min(1, (scale.GetValue(lifetime.To) - start) / (end - start)) : 1;

            if (to <= from)
            {
                continue;
            }

            if (spans.Count > 0 && from <= spans[^1].End)
            {
                spans[^1] = (spans[^1].Start, Math.Max(spans[^1].End, to));
            }
            else
            {
                spans.Add((from, to));
            }
        }

        return [.. spans];
    }

    private MemoryReferenceGraphMark[] GetMarks(MemoryHistoryGraphNode node, (double Start, double End) window)
    {
        var (start, end) = window;

        if (end <= start)
        {
            return [];
        }

        var scale = History.Scale;

        return
        [
            .. node.Marks
                   .Where(m => m.Position < History.ShutdownStart)
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

    private string GetTypeName(MemoryHistoryGraphTarget target)
    {
        if (target.Page is { } page)
        {
            return page.IsBuffer ? BufferType : FrameType;
        }

        if (!TypeNames.TryGetValue(target.Block, out var name))
        {
            name = History.GetTypeName(target.Block);

            TypeNames[target.Block] = name;
        }

        return name;
    }

    private string GetValueText(MemoryHistoryGraphTarget target, TracePosition position)
    {
        if (target.Page is { } page)
        {
            var name = History.GetBufferPageName(page);

            return page.Offset == 0 ? name : $"{name} {GetOffsetText(page.Offset)}";
        }

        var address = History.Store.Blocks[target.Block].Address;

        return History.IsBuffer(target.Block) && History.GetBufferPage(address, position) is { } buffer
            ? $"{History.GetBufferPageName(buffer)}  0x{address:X16}"
            : $"0x{address:X16}";
    }

    private static MemoryReferenceGraphEdge CreateEdge(MemoryReferenceGraphNode from,
                                                       MemoryReferenceGraphNode to,
                                                       List<MemoryHistoryGraphEdge> pointers,
                                                       bool isTree,
                                                       bool isReferrer)
    {
        var isUp = to.Y < from.Y;

        var portOffset = isTree ? 0 : isUp ? -LinkPortOffset : LinkPortOffset;

        var start = new Point(from.X + NodeWidth, from.Y + NodeHeight / 2 + portOffset);

        var isForward = to.X > from.X;

        var end = new Point(isForward ? to.X : to.X + NodeWidth, to.Y + NodeHeight / 2 - portOffset);

        var trunk = isReferrer ? end.X - TrunkGap : start.X + (isTree ? TrunkGap : isUp ? UpLinkTrunkGap : DownLinkTrunkGap);

        Point[] points = [start, new Point(trunk, start.Y), new Point(trunk, end.Y), end];

        var labelAt = isReferrer
            ? start
            : new Point(isForward ? Math.Max(start.X + TrunkGap, Math.Min(trunk, end.X)) : end.X, end.Y);

        var isLabelBelow = !isTree && isUp;

        var label = pointers.Count == 1 ? GetPointerText(pointers[0]) : $"{GetOffsetText(pointers[0].Offset)} ({pointers.Count})";

        var changes = pointers.Sum(p => p.ChangeCount);

        var detail = string.Join(Environment.NewLine, pointers.Select(p => $"{GetPointerText(p)}, {GetChangesText(p.ChangeCount)}"));

        return new MemoryReferenceGraphEdge(points,
                                            labelAt,
                                            label,
                                            changes > 0 && isTree ? GetChangesText(changes) : string.Empty,
                                            detail,
                                            changes,
                                            !isTree,
                                            isLabelBelow);
    }

    private static MemoryReferenceGraphEdge CreateLane(MemoryReferenceGraphNode from,
                                                       MemoryReferenceGraphNode to,
                                                       MemoryHistoryGraphEdge pointer,
                                                       double drop)
    {
        var start = new Point(from.X + NodeWidth, from.Y + NodeHeight / 2);

        var end = new Point(to.X, to.Y + NodeHeight / 2);

        var fanOut = start.X + TrunkGap;

        var fanIn = end.X - FanInGap;

        var lane = end.Y + drop;

        Point[] points = [start, new(fanOut, start.Y), new(fanOut, lane), new(fanIn, lane), new(fanIn, end.Y), end];

        return new MemoryReferenceGraphEdge(points,
                                            new Point(fanOut, lane),
                                            GetPointerText(pointer),
                                            pointer.ChangeCount > 0 ? GetChangesText(pointer.ChangeCount) : string.Empty,
                                            $"{GetPointerText(pointer)}, {GetChangesText(pointer.ChangeCount)}",
                                            pointer.ChangeCount,
                                            false,
                                            false);
    }

    private static string GetPointerText(MemoryHistoryGraphEdge pointer)
        => pointer.TargetOffset is > 0 and var offset
            ? $"{GetOffsetText(pointer.Offset)} → {GetOffsetText(offset)}"
            : GetOffsetText(pointer.Offset);

    private static string GetOffsetText(int offset) => $"+0x{offset:X}";

    private static string GetChangesText(int changes) => changes == 1 ? "1 Change" : $"{changes:N0} Changes";

    private static string GetDataChangesText(int changes)
        => changes switch
        {
            0 => string.Empty,
            1 => "1 Data Change",
            _ => $"{changes:N0} Data Changes"
        };
}
