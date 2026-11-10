using InternalsViewer.Query.FullTrace.Memory.Allocation;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimeline
{
    public TraceTimeline Filter(Func<int, bool> include) => new([.. Threads.Select(t => t.Filter(include))], this);

    public TraceTimeline? RootAt(Func<CallTreeNode, bool> isRoot)
    {
        var depths = new int[NodeCount];

        var rootDepths = new int[NodeCount];

        var any = false;

        for (var callNodeIndex = 0; callNodeIndex < depths.Length; callNodeIndex++)
        {
            var parent = Parents[callNodeIndex];

            depths[callNodeIndex] = parent >= 0 ? depths[parent] + 1 : 0;

            rootDepths[callNodeIndex] = parent >= 0 && rootDepths[parent] >= 0
                ? rootDepths[parent]
                : GetNode(callNodeIndex) is { } callNode && isRoot(callNode) ? depths[callNodeIndex] : -1;

            any |= rootDepths[callNodeIndex] >= 0;
        }

        if (!any)
        {
            return null;
        }

        var rooted = Threads.Select(t => Regroup(t, (_, row, index) => row.GetCallNodeIndex(index) is var n
                                                                       && n >= 0
                                                                       && n < depths.Length
                                                                       && rootDepths[n] >= 0
                                                                           ? depths[n] - rootDepths[n]
                                                                           : -1))
                            .ToList();

        var windows = GetWindows(rooted.Where(t => t is { CallCount: > 0, Rows.Count: > 0 }).Select(t => t.Rows[0]));

        var threads = new List<TraceTimelineThread>();

        for (var lane = 0; lane < Threads.Count; lane++)
        {
            var thread = rooted[lane].CallCount > 0 ? rooted[lane] : CropToWindows(Threads[lane], windows, depths);

            if (thread.CallCount > 0)
            {
                threads.Add(thread);
            }
        }

        return new TraceTimeline(threads, this);
    }

    public TraceTimeline? CropTo(IReadOnlySet<CallTreeNode> roots)
    {
        var known = new Dictionary<CallTreeNode, int>(ReferenceEqualityComparer.Instance);

        var depths = new int[NodeCount];

        for (var callNodeIndex = 0; callNodeIndex < depths.Length; callNodeIndex++)
        {
            depths[callNodeIndex] = GetNode(callNodeIndex) is { } callNode ? GetDepthUnder(roots, callNode, known) : -1;
        }

        List<TraceTimelineThread> threads = [.. Threads.Select(t => Regroup(t, (_, row, index) => GetRowDepth(row, index)))
                                                       .Where(t => t.CallCount > 0)];

        return threads.Count > 0 ? new TraceTimeline(threads, this, fit: true) : null;

        int GetRowDepth(TraceTimelineRow row, int index)
            => row.GetCallNodeIndex(index) is var callNodeIndex && callNodeIndex >= 0 && callNodeIndex < depths.Length ? depths[callNodeIndex] : -1;
    }

    public TraceTimeline Fold(Func<CallTreeNode, bool> isFolded)
    {
        var folded = new bool[NodeCount];

        var any = false;

        for (var callNodeIndex = 0; callNodeIndex < folded.Length; callNodeIndex++)
        {
            folded[callNodeIndex] = GetNode(callNodeIndex) is { } callNode && isFolded(callNode);

            any |= folded[callNodeIndex];
        }

        if (!any)
        {
            return this;
        }

        var lifts = new int[NodeCount];

        int GetLift(int callNodeIndex, int depth)
        {
            if (depth == 0 || callNodeIndex < 0 || callNodeIndex >= lifts.Length)
            {
                return 0;
            }

            if (lifts[callNodeIndex] < 0)
            {
                var parent = Parents[callNodeIndex];

                lifts[callNodeIndex] = parent < 0 ? 0 : GetLift(parent, depth - 1) + (folded[parent] ? 1 : 0);
            }

            return lifts[callNodeIndex];
        }

        var threads = new List<TraceTimelineThread>(Threads.Count);

        foreach (var thread in Threads)
        {
            Array.Fill(lifts, -1);

            threads.Add(Regroup(thread, (depth, row, index) => row.GetCallNodeIndex(index) is var n && n >= 0 && n < folded.Length && folded[n]
                                                                   ? -1
                                                                   : depth - GetLift(n, depth)));
        }

        return new TraceTimeline(threads, this);
    }

    private static List<(TracePosition Start, TracePosition End)> GetWindows(IEnumerable<TraceTimelineRow> roots)
    {
        var windows = new List<(TracePosition Start, TracePosition End)>();

        foreach (var row in roots)
        {
            for (var index = 0; index < row.Count; index++)
            {
                windows.Add((row.GetFrom(index).Position, row.GetTo(index).Position));
            }
        }

        windows.Sort();

        var merged = new List<(TracePosition Start, TracePosition End)>();

        foreach (var window in windows)
        {
            if (merged.Count > 0 && window.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, window.End > merged[^1].End ? window.End : merged[^1].End);
            }
            else
            {
                merged.Add(window);
            }
        }

        return merged;
    }

    private static int GetDepthUnder(IReadOnlySet<CallTreeNode> roots, CallTreeNode node, Dictionary<CallTreeNode, int> known)
    {
        if (known.TryGetValue(node, out var depth))
        {
            return depth;
        }

        if (node.Parent is not { } parent || !parent.Children.TryGetValue(node.Key, out var child) || !ReferenceEquals(child, node))
        {
            depth = -1;
        }
        else if (roots.Contains(node))
        {
            depth = 0;
        }
        else
        {
            var parentDepth = GetDepthUnder(roots, parent, known);

            depth = parentDepth < 0 ? -1 : parentDepth + 1;
        }

        known[node] = depth;

        return depth;
    }

    private TraceTimelineThread CropToWindows(TraceTimelineThread thread, List<(TracePosition Start, TracePosition End)> windows, int[] depths)
    {
        TracePosition[] windowEnds = [.. windows.Select(w => w.End)];

        var shallowest = int.MaxValue;

        foreach (var row in thread.Rows)
        {
            for (var index = 0; index < row.Count; index++)
            {
                if (Overlaps(row, index))
                {
                    shallowest = Math.Min(shallowest, GetRowDepth(row, index));
                }
            }
        }

        return shallowest == int.MaxValue
            ? new TraceTimelineThread(thread.ThreadId, thread.Clock, thread.Store, [], 0)
            : Regroup(thread, (_, row, index) => Overlaps(row, index) ? GetRowDepth(row, index) - shallowest : -1);

        bool Overlaps(TraceTimelineRow row, int index)
        {
            var first = SortedSearch.FirstAtOrAfter(windowEnds, row.GetFrom(index).Position);

            return first < windows.Count && windows[first].Start <= row.GetTo(index).Position;
        }

        int GetRowDepth(TraceTimelineRow row, int index)
            => row.GetCallNodeIndex(index) is var callNodeIndex && callNodeIndex >= 0 && callNodeIndex < depths.Length ? depths[callNodeIndex] : 0;
    }

    private TraceTimelineThread Regroup(TraceTimelineThread thread, Func<int, TraceTimelineRow, int, int> getDepth)
    {
        var sources = new List<RowSource>();

        for (var source = 0; source < thread.Rows.Count; source++)
        {
            var row = thread.Rows[source];

            for (var index = 0; index < row.Count; index++)
            {
                var depth = getDepth(source, row, index);

                if (depth < 0)
                {
                    continue;
                }

                while (sources.Count <= depth)
                {
                    sources.Add(new RowSource());
                }

                sources[depth].Add(source, index);
            }
        }

        var indexes = sources.Select(s => s is { Count: > 0, IsSlice: true } ? null : new int[s.Count]).ToArray();

        if (sources.Any(s => s is { Count: > 0, IsSlice: false }))
        {
            Merge(thread, getDepth, indexes);
        }

        TraceTimelineRow[] rows = [.. sources.Select((s, depth) => indexes[depth] is { } rowIndexes
                                                                   ? new TraceTimelineRow(Scale, thread.Clock, thread.Store, rowIndexes)
                                                                   : thread.Rows[s.Row].Slice(s.First, s.Count))];

        return new TraceTimelineThread(thread.ThreadId, thread.Clock, thread.Store, rows, sources.Sum(s => s.Count));
    }

    private static void Merge(TraceTimelineThread thread, Func<int, TraceTimelineRow, int, int> getDepth, int[]?[] indexes)
    {
        var rows = thread.Rows;

        var filled = new int[indexes.Length];

        var cursors = new PriorityQueue<(int Source, int Index), TracePosition>(rows.Count);

        for (var source = 0; source < rows.Count; source++)
        {
            if (rows[source].Count > 0)
            {
                cursors.Enqueue((source, 0), rows[source].GetFromPosition(0));
            }
        }

        while (cursors.TryDequeue(out var cursor, out _))
        {
            var (source, index) = cursor;

            var row = rows[source];

            if (index + 1 < row.Count)
            {
                cursors.Enqueue((source, index + 1), row.GetFromPosition(index + 1));
            }

            var depth = getDepth(source, row, index);

            if (depth < 0 || indexes[depth] is not { } target)
            {
                continue;
            }

            target[filled[depth]++] = row.GetStoreIndex(index);
        }
    }

    private sealed class RowSource
    {
        public int Count { get; private set; }

        public int Row { get; private set; } = -1;

        public int First { get; private set; }

        public bool IsSlice { get; private set; } = true;

        public void Add(int row, int index)
        {
            if (Count == 0)
            {
                Row = row;

                First = index;
            }
            else if (row != Row || index != First + Count)
            {
                IsSlice = false;
            }

            Count++;
        }
    }
}
