using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History.Graph;

public sealed class MemoryHistoryGraph
{
    public const char PathSeparator = '/';

    public const char ReferrerSeparator = '<';

    private const int PointerSize = MemoryHistoryPointerReader.PointerSize;

    private MemoryHistoryGraph(List<MemoryHistoryGraphNode> nodes,
                               List<MemoryHistoryGraphEdge> edges,
                               Dictionary<(int From, int To, int Offset), TargetOffset[]> targetOffsets,
                               bool isTruncated)
    {
        Nodes = nodes;

        Edges = edges;

        TargetOffsets = targetOffsets;

        IsTruncated = isTruncated;
    }

    public static MemoryHistoryGraph Empty { get; } = new([], [], [], false);

    public List<MemoryHistoryGraphNode> Nodes { get; }

    public List<MemoryHistoryGraphEdge> Edges { get; }

    public bool IsTruncated { get; }

    private Dictionary<(int From, int To, int Offset), TargetOffset[]> TargetOffsets { get; }

    public static MemoryHistoryGraph Build(MemoryHistoryStore store,
                                           TraceBufferPool pool,
                                           int block,
                                           TracePosition from,
                                           TracePosition to,
                                           TracePosition shutdown,
                                           Func<string, bool> isExpanded,
                                           Func<string, bool> isReferrersExpanded,
                                           int depth,
                                           int limit,
                                           Func<int, MemoryHistoryArray?>? getArray = null)
    {
        var root = store.Blocks[block];

        MemoryHistoryGraphTarget[] rootTargets = [new(root.FromPosition, root.ToPosition, root.Address, block, null)];

        return Build(store, pool, rootTargets, null, from, to, shutdown, isExpanded, isReferrersExpanded, depth, limit, getArray);
    }

    public static MemoryHistoryGraph BuildFromCall(MemoryHistoryStore store,
                                                   TraceBufferPool pool,
                                                   ulong[] arguments,
                                                   TracePosition position,
                                                   TracePosition from,
                                                   TracePosition to,
                                                   TracePosition shutdown,
                                                   Func<string, bool> isExpanded,
                                                   Func<string, bool> isReferrersExpanded,
                                                   int depth,
                                                   int limit,
                                                   Func<int, MemoryHistoryArray?>? getArray = null)
    {
        var seeds = new SortedDictionary<int, List<SlotSegment>>();

        for (var index = 0; index < arguments.Length; index++)
        {
            var value = arguments[index];

            if (value == 0 || MemoryHistoryPointerReader.GetTarget(store, pool, value, position) is not { } target)
            {
                continue;
            }

            var end = GetTargetEnd(store, pool, target, value, position);

            seeds[index] = [new SlotSegment(new MemoryHistoryGraphTarget(position, end, value, target.Block, target.Page), -1, position)];
        }

        return Build(store, pool, [], seeds, from, to, shutdown, isExpanded, isReferrersExpanded, depth, limit, getArray);
    }

    public MemoryHistoryGraph GetHot(bool isReferenceHot, bool isDataHot)
    {
        if (Nodes.Count == 0 || (!isReferenceHot && !isDataHot))
        {
            return this;
        }

        var isHot = new bool[Nodes.Count];

        if (isReferenceHot)
        {
            foreach (var edge in Edges.Where(e => e.ChangeCount > 0 && IsTreeEdge(e)))
            {
                isHot[Nodes[edge.To].Parent == edge.From ? edge.To : edge.From] = true;
            }
        }

        if (isDataHot)
        {
            for (var index = 0; index < Nodes.Count; index++)
            {
                isHot[index] |= Nodes[index].DataChangeCount > 0;
            }
        }

        var isKept = new bool[Nodes.Count];

        var shown = new HashSet<string>();

        Keep(isKept, shown, 0);

        for (var index = 1; index < Nodes.Count; index++)
        {
            if (isHot[index] && !Nodes[index].Targets.All(t => shown.Contains(GetTargetKey(t))))
            {
                Keep(isKept, shown, index);
            }
        }

        var indexes = new int[Nodes.Count];

        var nodes = new List<MemoryHistoryGraphNode>();

        for (var index = 0; index < Nodes.Count; index++)
        {
            indexes[index] = isKept[index] ? nodes.Count : -1;

            if (isKept[index])
            {
                var node = Nodes[index];

                nodes.Add(new MemoryHistoryGraphNode(node.Path,
                                                     node.Depth,
                                                     node.Parent >= 0 ? indexes[node.Parent] : -1,
                                                     node.Targets,
                                                     node.Marks)
                {
                    HasPointers = node.HasPointers,
                    HasReferrers = node.HasReferrers,
                    IsReferrer = node.IsReferrer,
                    Object = node.Object
                });
            }
        }

        var edges = Edges.Where(e => isKept[e.From] && isKept[e.To] && IsTreeEdge(e))
                         .Select(e => e with { From = indexes[e.From], To = indexes[e.To] })
                         .ToList();

        var targetOffsets = TargetOffsets.Where(o => isKept[o.Key.From] && isKept[o.Key.To])
                                         .ToDictionary(o => (indexes[o.Key.From], indexes[o.Key.To], o.Key.Offset), o => o.Value);

        return new MemoryHistoryGraph(nodes, edges, targetOffsets, IsTruncated);
    }

    public int? GetTargetOffset(MemoryHistoryGraphEdge edge, TracePosition position)
    {
        if (edge.TargetOffset is { } offset)
        {
            return offset;
        }

        if (!TargetOffsets.TryGetValue((edge.From, edge.To, edge.Offset), out var offsets))
        {
            return null;
        }

        var index = SortedSearch.FirstAfter<TargetOffset, TracePosition>(offsets, position, o => o.From) - 1;

        return index >= 0 && position < offsets[index].To ? offsets[index].Offset : null;
    }

    public int? GetMaximumTargetOffset(MemoryHistoryGraphEdge edge)
        => edge.TargetOffset
           ?? (TargetOffsets.TryGetValue((edge.From, edge.To, edge.Offset), out var offsets) ? offsets.Max(o => o.Offset) : null);

    public bool IsTreeEdge(MemoryHistoryGraphEdge edge)
        => edge.From != edge.To
           && (Nodes[edge.To].Parent == edge.From || (Nodes[edge.From].IsReferrer && Nodes[edge.From].Parent == edge.To));

    private static MemoryHistoryGraph Build(MemoryHistoryStore store,
                                            TraceBufferPool pool,
                                            MemoryHistoryGraphTarget[] rootTargets,
                                            SortedDictionary<int, List<SlotSegment>>? seeds,
                                            TracePosition from,
                                            TracePosition to,
                                            TracePosition shutdown,
                                            Func<string, bool> isExpanded,
                                            Func<string, bool> isReferrersExpanded,
                                            int depth,
                                            int limit,
                                            Func<int, MemoryHistoryArray?>? getArray)
    {
        var changesEnd = shutdown < to ? shutdown : to;

        var changes = new Dictionary<int, Dictionary<int, SlotChangeList>>();

        var nodes = new List<MemoryHistoryGraphNode>
        {
            new(string.Empty, 0, -1, rootTargets, GetDataMarks(store, pool, rootTargets, from, changesEnd, changes))
        };

        var edges = new List<MemoryHistoryGraphEdge>();

        var targetOffsets = new Dictionary<(int From, int To, int Offset), TargetOffset[]>();

        var owners = new Dictionary<string, int>();

        Own(owners, rootTargets, 0);

        var timelines = new Dictionary<int, Dictionary<int, List<MemoryHistoryGraphTarget>>>();

        var queue = new Queue<int>([0]);

        var isTruncated = false;

        while (queue.TryDequeue(out var index))
        {
            var node = nodes[index];

            if (node.Depth >= depth || !isExpanded(node.Path))
            {
                continue;
            }

            if (node.Element is null && GetArray(node, getArray) is { } array)
            {
                isTruncated |= AddElements(store, nodes, edges, queue, index, array, limit);

                continue;
            }

            foreach (var (offset, segments) in index == 0 && seeds is not null ? seeds : GetSlots(store, pool, node, from, to, timelines))
            {
                var field = node.Element is { } element ? offset - element.Offset : offset;

                List<MemoryHistoryGraphTarget> targets = [.. segments.Select(s => s.Target)];

                var moves = GetMoves(store, pool, segments, offset, from, changesEnd, changes);

                var changeCount = moves.Count;

                if (targets.All(t => owners.ContainsKey(GetTargetKey(t))))
                {
                    foreach (var owned in targets.GroupBy(t => owners[GetTargetKey(t)]))
                    {
                        AddEdge(store, edges, targetOffsets, index, owned.Key, field, [.. owned], changeCount);
                    }

                    continue;
                }

                if (nodes.Count >= limit)
                {
                    isTruncated = true;

                    continue;
                }

                var target = nodes.Count;

                nodes.Add(new MemoryHistoryGraphNode($"{node.Path}{PathSeparator}{field:X}",
                                                     node.Depth + 1,
                                                     index,
                                                     [.. targets],
                                                     [.. moves, .. GetDataMarks(store, pool, targets, from, changesEnd, changes)]));

                Own(owners, targets, target);

                queue.Enqueue(target);

                AddEdge(store, edges, targetOffsets, index, target, field, targets, changeCount);
            }
        }

        var placed = nodes.SelectMany(n => GetBlocks(n.Targets)).ToHashSet();

        var level = Enumerable.Range(0, nodes.Count).Where(i => !nodes[i].IsData && isReferrersExpanded(nodes[i].Path)).ToList();

        for (var step = 0; level.Count > 0 && step < depth; step++)
        {
            var next = new List<int>();

            var references = GetReferences(store, pool, [.. level.SelectMany(i => GetBlocks(nodes[i].Targets))], from, to);

            foreach (var index in level)
            {
                var node = nodes[index];

                var blocks = GetBlocks(node.Targets).ToHashSet();

                var found = references.Where(r => blocks.Contains(r.Target.Block) && !placed.Contains(r.Block)).ToList();

                foreach (var (key, lane) in GetReferrerLanes(store, found))
                {
                    if (nodes.Count >= limit)
                    {
                        isTruncated = true;

                        continue;
                    }

                    var referrer = nodes.Count;

                    var path = $"{node.Path}{ReferrerSeparator}{key}";

                    var targets = GetReferrerTargets(store, lane);

                    nodes.Add(new MemoryHistoryGraphNode(path,
                                                         node.Depth - 1,
                                                         index,
                                                         targets,
                                                         GetDataMarks(store, pool, targets, from, changesEnd, changes))
                    {
                        IsReferrer = true
                    });

                    placed.UnionWith(lane.Select(r => r.Block));

                    foreach (var slot in lane.GroupBy(r => r.Offset).OrderBy(g => g.Key))
                    {
                        List<SlotSegment> pointers = [.. slot.Select(r => new SlotSegment(r.Target, r.Block, r.Target.From))];

                        AddEdge(store,
                                edges,
                                targetOffsets,
                                referrer,
                                index,
                                slot.Key,
                                [.. slot.Select(r => r.Target)],
                                GetMoves(store, pool, pointers, slot.Key, from, changesEnd, changes).Count);
                    }

                    if (isReferrersExpanded(path))
                    {
                        next.Add(referrer);
                    }
                }
            }

            level = next;
        }

        foreach (var node in nodes.Where(n => !n.IsReferrer))
        {
            node.HasPointers = GetSlots(store, pool, node, from, to, timelines).Count > 0 || (node.Depth == 0 && seeds is { Count: > 0 });
        }

        foreach (var node in nodes.Where(n => n.Targets.Length > 0 && n.Targets.All(t => t.Page is null && t.Block == n.Targets[0].Block)))
        {
            var info = store.Blocks[node.Targets[0].Block];

            node.Object = new MemoryHistoryGraphTarget(info.FromPosition, info.ToPosition, info.Address, node.Targets[0].Block, null);
        }

        var unplaced = GetReferences(store, pool, [.. nodes.SelectMany(n => GetBlocks(n.Targets))], from, to)
                       .Where(r => !placed.Contains(r.Block))
                       .Select(r => r.Target.Block)
                       .ToHashSet();

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];

            node.HasReferrers = nodes.Any(n => n.IsReferrer && n.Parent == index) || GetBlocks(node.Targets).Any(unplaced.Contains);
        }

        return new MemoryHistoryGraph(nodes, edges, targetOffsets, isTruncated);
    }

    private void Keep(bool[] isKept, HashSet<string> shown, int node)
    {
        for (var index = node; index >= 0 && !isKept[index]; index = Nodes[index].Parent)
        {
            isKept[index] = true;

            shown.UnionWith(Nodes[index].Targets.Select(GetTargetKey));
        }
    }

    private static SortedDictionary<int, List<SlotSegment>> GetSlots(
        MemoryHistoryStore store,
        TraceBufferPool pool,
        MemoryHistoryGraphNode node,
        TracePosition from,
        TracePosition to,
        Dictionary<int, Dictionary<int, List<MemoryHistoryGraphTarget>>> timelines)
    {
        var slots = new SortedDictionary<int, List<SlotSegment>>();

        foreach (var instance in node.Targets.Where(t => t.Block >= 0))
        {
            if (!timelines.TryGetValue(instance.Block, out var timeline))
            {
                timeline = GetTimeline(store, pool, instance.Block, from, to);

                timelines[instance.Block] = timeline;
            }

            foreach (var (offset, segments) in timeline)
            {
                if (node.Element is { } element && !element.Contains(offset))
                {
                    continue;
                }

                foreach (var segment in segments)
                {
                    if (segment.From >= instance.To || segment.To <= instance.From)
                    {
                        continue;
                    }

                    if (!slots.TryGetValue(offset, out var targets))
                    {
                        targets = [];

                        slots[offset] = targets;
                    }

                    var visible = segment with
                    {
                        From = segment.From > instance.From ? segment.From : instance.From,
                        To = segment.To < instance.To ? segment.To : instance.To
                    };

                    targets.Add(new SlotSegment(visible, instance.Block, segment.From));
                }
            }
        }

        return slots;
    }

    private static MemoryHistoryArray? GetArray(MemoryHistoryGraphNode node, Func<int, MemoryHistoryArray?>? getArray)
        => getArray is not null
           && node.Targets.Length > 0
           && node.Targets.All(t => t.Page is null && t.Block >= 0 && t.Block == node.Targets[0].Block)
           && getArray(node.Targets[0].Block) is { Count: > 0, ElementSize: > 0 } array
            ? array
            : null;

    private static bool AddElements(MemoryHistoryStore store,
                                    List<MemoryHistoryGraphNode> nodes,
                                    List<MemoryHistoryGraphEdge> edges,
                                    Queue<int> queue,
                                    int index,
                                    MemoryHistoryArray array,
                                    int limit)
    {
        var node = nodes[index];

        var address = store.Blocks[node.Targets[0].Block].Address;

        for (var element = 0; element < array.Count; element++)
        {
            if (nodes.Count >= limit)
            {
                return true;
            }

            var offset = element * array.ElementSize;

            var target = nodes.Count;

            nodes.Add(new MemoryHistoryGraphNode($"{node.Path}{PathSeparator}{offset:X}",
                                                 node.Depth + 1,
                                                 index,
                                                 [.. node.Targets.Select(t => t with { Value = address + (ulong)offset })],
                                                 [])
            {
                Element = new MemoryHistoryGraphElement(element, offset, array.ElementSize)
            });

            queue.Enqueue(target);

            edges.Add(new MemoryHistoryGraphEdge(index, target, offset, 0, 0));
        }

        return false;
    }

    private static IEnumerable<int> GetBlocks(IEnumerable<MemoryHistoryGraphTarget> targets)
        => targets.Where(t => t.Block >= 0).Select(t => t.Block).Distinct();

    private static List<Reference> GetReferences(MemoryHistoryStore store,
                                                 TraceBufferPool pool,
                                                 HashSet<int> blocks,
                                                 TracePosition from,
                                                 TracePosition to)
    {
        var ranges = GetRanges(store, blocks);

        var starts = Array.ConvertAll(ranges, r => r.Start);

        var references = new List<Reference>();

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            var info = store.Blocks[block];

            if (blocks.Contains(block) || info.WriteCount == 0 || info.FromPosition > to || info.ToPosition <= from)
            {
                continue;
            }

            var timeline = GetTimeline(store, pool, block, from, to, v => IsInRanges(ranges, starts, v));

            foreach (var (offset, segments) in timeline)
            {
                references.AddRange(segments.Where(s => blocks.Contains(s.Block)).Select(s => new Reference(block, offset, s)));
            }
        }

        return references;
    }

    private static (ulong Start, ulong End)[] GetRanges(MemoryHistoryStore store, HashSet<int> blocks)
        => [.. IntervalHelper.Coalesce(blocks.Select(b => store.Blocks[b]), b => b.Address, b => b.Address + (ulong)b.Size)];

    private static bool IsInRanges((ulong Start, ulong End)[] ranges, ulong[] starts, ulong value)
    {
        var index = Array.BinarySearch(starts, value);

        if (index < 0)
        {
            index = ~index - 1;
        }

        return index >= 0 && value < ranges[index].End;
    }

    private static List<(string Key, List<Reference> References)> GetReferrerLanes(MemoryHistoryStore store, List<Reference> references)
    {
        var lanes = new List<(string Key, List<Reference> References)>();

        foreach (var site in references.GroupBy(r => GetAllocationKey(store, r.Block)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var ends = new List<TracePosition>();

            var siteLanes = new List<List<Reference>>();

            foreach (var referrer in site.GroupBy(r => r.Block).OrderBy(g => g.Min(r => r.Target.From)))
            {
                var start = referrer.Min(r => r.Target.From);

                var end = referrer.Max(r => r.Target.To);

                var lane = ends.FindIndex(e => e <= start);

                if (lane < 0)
                {
                    lane = ends.Count;

                    ends.Add(end);

                    siteLanes.Add([]);
                }
                else
                {
                    ends[lane] = end;
                }

                siteLanes[lane].AddRange(referrer);
            }

            for (var index = 0; index < siteLanes.Count; index++)
            {
                lanes.Add(($"{site.Key}.{index}", siteLanes[index]));
            }
        }

        return lanes;
    }

    private static string GetAllocationKey(MemoryHistoryStore store, int block)
        => store.Blocks[block] is { CallNodeIndex: >= 0 } info ? $"{info.CallNodeIndex:X}:{info.Size:X}" : $"#{block:X}";

    private static MemoryHistoryGraphTarget[] GetReferrerTargets(MemoryHistoryStore store, List<Reference> references)
    {
        var targets = new List<MemoryHistoryGraphTarget>();

        foreach (var referrer in references.GroupBy(r => r.Block))
        {
            var address = store.Blocks[referrer.Key].Address;

            foreach (var (start, end) in IntervalHelper.Coalesce(referrer.Select(r => r.Target), t => t.From, t => t.To))
            {
                targets.Add(new MemoryHistoryGraphTarget(start, end, address, referrer.Key, null));
            }
        }

        return [.. targets.OrderBy(t => t.From)];
    }

    private static Dictionary<int, List<MemoryHistoryGraphTarget>> GetTimeline(MemoryHistoryStore store,
                                                                               TraceBufferPool pool,
                                                                               int block,
                                                                               TracePosition from,
                                                                               TracePosition to,
                                                                               Func<ulong, bool>? isWanted = null)
    {
        var info = store.Blocks[block];

        var data = new byte[info.Size];

        var known = new bool[info.Size];

        var first = (int)((PointerSize - info.Address % PointerSize) % PointerSize);

        var count = Math.Max(0, (info.Size - first) / PointerSize);

        var written = new TracePosition[count];

        var values = new ulong?[count];

        var timeline = new Dictionary<int, List<MemoryHistoryGraphTarget>>();

        var writes = store.GetWrites(block);

        var index = 0;

        for (; index < writes.Count && writes[index].Position <= from; index++)
        {
            var write = writes[index];

            store.Apply(write, data, known);

            foreach (var slot in GetTouchedSlots(write, first, count))
            {
                written[slot] = write.Position;
            }
        }

        for (var slot = 0; slot < count; slot++)
        {
            values[slot] = Read(data, known, first + slot * PointerSize);

            if (values[slot] is { } value)
            {
                Open(store, pool, block, timeline, first + slot * PointerSize, value, written[slot], from, isWanted);
            }
        }

        for (; index < writes.Count && writes[index].Position <= to; index++)
        {
            var write = writes[index];

            store.Apply(write, data, known);

            foreach (var slot in GetTouchedSlots(write, first, count))
            {
                var offset = first + slot * PointerSize;

                if (Read(data, known, offset) is not { } value || value == values[slot])
                {
                    continue;
                }

                values[slot] = value;

                if (timeline.TryGetValue(offset, out var segments) && segments[^1].To > write.Position)
                {
                    segments[^1] = segments[^1] with { To = write.Position };
                }

                Open(store, pool, block, timeline, offset, value, write.Position, from, isWanted);
            }
        }

        if (!timeline.ContainsKey(BufferDescriptor.FrameOffset) && pool.IsBuffer(info.Address))
        {
            AddFrames(pool, timeline, info.Address, from, to, isWanted);
        }

        return timeline;
    }

    private static void AddFrames(TraceBufferPool pool,
                                  Dictionary<int, List<MemoryHistoryGraphTarget>> timeline,
                                  ulong buffer,
                                  TracePosition from,
                                  TracePosition to,
                                  Func<ulong, bool>? isWanted)
    {
        var residencies = pool.GetResidencies(buffer);

        var frames = new List<MemoryHistoryGraphTarget>();

        for (var index = 0; index < residencies.Count; index++)
        {
            var residency = residencies[index];

            var end = index + 1 < residencies.Count ? residencies[index + 1].Position : TracePosition.Unknown;

            if (residency.Position > to || end <= from || isWanted?.Invoke(residency.Frame) == false)
            {
                continue;
            }

            var page = new TraceBufferReference(residency.Page, residency.DatabaseId, 0);

            frames.Add(new MemoryHistoryGraphTarget(residency.Position, end, residency.Frame, -1, page));
        }

        if (frames.Count > 0)
        {
            timeline[BufferDescriptor.FrameOffset] = frames;
        }
    }

    private static TracePosition GetTargetEnd(MemoryHistoryStore store,
                                              TraceBufferPool pool,
                                              (int Block, TraceBufferReference? Page) target,
                                              ulong value,
                                              TracePosition position)
        => target.Page is { } page ? pool.GetReferenceEnd(page, value, position) : store.Blocks[target.Block].ToPosition;

    private static IEnumerable<int> GetTouchedSlots(MemoryHistoryWrite write, int first, int count)
    {
        var start = Math.Max(0, (write.Offset - first) / PointerSize);

        var end = Math.Min(count, (write.Offset + write.Length - first + PointerSize - 1) / PointerSize);

        for (var slot = start; slot < end; slot++)
        {
            yield return slot;
        }
    }

    private static ulong? Read(byte[] data, bool[] known, int offset)
        => known.AsSpan(offset, PointerSize).Contains(false) ? null : BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset));

    private static void Open(MemoryHistoryStore store,
                             TraceBufferPool pool,
                             int block,
                             Dictionary<int, List<MemoryHistoryGraphTarget>> timeline,
                             int offset,
                             ulong value,
                             TracePosition position,
                             TracePosition from,
                             Func<ulong, bool>? isWanted)
    {
        if (value == 0
            || isWanted?.Invoke(value) == false
            || MemoryHistoryPointerReader.GetTarget(store, pool, value, position) is not { } target
            || target.Block == block)
        {
            return;
        }

        var end = GetTargetEnd(store, pool, target, value, position);

        if (end <= position || end <= from)
        {
            return;
        }

        if (!timeline.TryGetValue(offset, out var segments))
        {
            segments = [];

            timeline[offset] = segments;
        }

        segments.Add(new MemoryHistoryGraphTarget(position, end, value, target.Block, target.Page));
    }

    private static void Own(Dictionary<string, int> owners, IEnumerable<MemoryHistoryGraphTarget> targets, int node)
    {
        foreach (var target in targets)
        {
            owners.TryAdd(GetTargetKey(target), node);
        }
    }

    private static string GetTargetKey(MemoryHistoryGraphTarget target)
        => target.Page is { } page ? $"{page.DatabaseId}:{page.Page}:{page.IsBuffer}" : $"{target.Block}";

    private static void AddEdge(MemoryHistoryStore store,
                                List<MemoryHistoryGraphEdge> edges,
                                Dictionary<(int From, int To, int Offset), TargetOffset[]> targetOffsets,
                                int from,
                                int to,
                                int offset,
                                List<MemoryHistoryGraphTarget> targets,
                                int changeCount)
    {
        var targetOffset = GetTargetOffset(store, targets);

        edges.Add(new MemoryHistoryGraphEdge(from, to, offset, targetOffset, changeCount));

        if (targetOffset is null && targets.Count > 0)
        {
            targetOffsets[(from, to, offset)] =
            [
                .. targets.Select(t => new TargetOffset(t.From, t.To, GetOffset(store, t))).OrderBy(t => t.From)
            ];
        }
    }

    private static int GetOffset(MemoryHistoryStore store, MemoryHistoryGraphTarget target)
        => target.Page is { } page ? page.Offset : (int)(target.Value - store.Blocks[target.Block].Address);

    private static int? GetTargetOffset(MemoryHistoryStore store, List<MemoryHistoryGraphTarget> targets)
    {
        int? found = null;

        foreach (var target in targets)
        {
            var offset = GetOffset(store, target);

            if (found is { } previous && previous != offset)
            {
                return null;
            }

            found = offset;
        }

        return found;
    }

    private static List<MemoryHistoryGraphMark> GetMoves(MemoryHistoryStore store,
                                                         TraceBufferPool pool,
                                                         List<SlotSegment> segments,
                                                         int offset,
                                                         TracePosition from,
                                                         TracePosition to,
                                                         Dictionary<int, Dictionary<int, SlotChangeList>> changes)
    {
        var moves = new List<MemoryHistoryGraphMark>();

        var counted = new HashSet<(int Owner, TracePosition Origin)>();

        ulong? previous = null;

        foreach (var segment in segments.OrderBy(s => s.Target.From))
        {
            if (previous is { } last
                && last != segment.Target.Value
                && segment.Owner >= 0
                && GetChangeCounts(store, pool, segment.Owner, from, to, changes).TryGetValue(offset, out var slot)
                && slot.References.TryGetValue(segment.Origin, out var write)
                && counted.Add((segment.Owner, segment.Origin)))
            {
                moves.Add(new MemoryHistoryGraphMark(segment.Target.From, write.CallNodeIndex, true));
            }

            previous = segment.Target.Value;
        }

        return moves;
    }

    private static List<MemoryHistoryGraphMark> GetDataMarks(MemoryHistoryStore store,
                                                             TraceBufferPool pool,
                                                             IEnumerable<MemoryHistoryGraphTarget> targets,
                                                             TracePosition from,
                                                             TracePosition to,
                                                             Dictionary<int, Dictionary<int, SlotChangeList>> changes)
    {
        var marks = new List<MemoryHistoryGraphMark>();

        foreach (var block in GetBlocks(targets))
        {
            var writes = GetChangeCounts(store, pool, block, from, to, changes).Values.SelectMany(s => s.Data).DistinctBy(w => w.Position);

            marks.AddRange(writes.Select(w => new MemoryHistoryGraphMark(w.Position, w.CallNodeIndex, false)));
        }

        return marks;
    }

    private static Dictionary<int, SlotChangeList> GetChangeCounts(MemoryHistoryStore store,
                                                                TraceBufferPool pool,
                                                                int block,
                                                                TracePosition from,
                                                                TracePosition to,
                                                                Dictionary<int, Dictionary<int, SlotChangeList>> changes)
    {
        if (!changes.TryGetValue(block, out var counts))
        {
            counts = CountChanges(store, pool, block, from, to);

            changes[block] = counts;
        }

        return counts;
    }

    private static Dictionary<int, SlotChangeList> CountChanges(MemoryHistoryStore store,
                                                             TraceBufferPool pool,
                                                             int block,
                                                             TracePosition from,
                                                             TracePosition to)
    {
        var info = store.Blocks[block];

        var data = new byte[info.Size];

        var known = new bool[info.Size];

        var first = (int)((PointerSize - info.Address % PointerSize) % PointerSize);

        var count = Math.Max(0, (info.Size - first) / PointerSize);

        var values = new ulong?[count];

        var isPointer = new bool[count];

        var references = new ulong?[count];

        var setters = new MemoryHistoryWrite?[count];

        var referenceChanges = new List<MemoryHistoryWrite>?[count];

        var dataChanges = new List<MemoryHistoryWrite>?[count];

        foreach (var write in store.GetWrites(block))
        {
            store.Apply(write, data, known);

            foreach (var slot in GetTouchedSlots(write, first, count))
            {
                var value = Read(data, known, first + slot * PointerSize);

                if (value == values[slot])
                {
                    continue;
                }

                values[slot] = value;

                var wasPointer = isPointer[slot];

                var target = value is { } pointer and not 0
                    ? MemoryHistoryPointerReader.GetTarget(store, pool, pointer, write.Position)
                    : null;

                isPointer[slot] = target is not null;

                bool isSetup;

                if (setters[slot] is not { } setter)
                {
                    isSetup = true;

                    if (value is not (null or 0))
                    {
                        setters[slot] = write;
                    }
                }
                else
                {
                    isSetup = IsSameCall(write, setter);
                }

                if (target is { } found && found.Block != block)
                {
                    if (!isSetup && references[slot] is { } previous && previous != value)
                    {
                        (referenceChanges[slot] ??= []).Add(write);
                    }

                    references[slot] = value;
                }
                else if (!wasPointer && !isPointer[slot] && !isSetup)
                {
                    (dataChanges[slot] ??= []).Add(write);
                }
            }
        }

        var counts = new Dictionary<int, SlotChangeList>();

        for (var slot = 0; slot < count; slot++)
        {
            var writes = dataChanges[slot] ?? [];

            if (values[slot] == 0 && writes.Count > 0)
            {
                var end = writes.Count - 1;

                while (end > 0 && IsSameCall(writes[end - 1], writes[^1]))
                {
                    end--;
                }

                writes.RemoveRange(end, writes.Count - end);
            }

            var moved = (referenceChanges[slot] ?? []).Where(w => w.Position > from && w.Position < to).ToDictionary(w => w.Position);

            List<MemoryHistoryWrite> changed = [.. writes.Where(w => w.Position > from && w.Position < to)];

            if (moved.Count + changed.Count > 0)
            {
                counts[first + slot * PointerSize] = new SlotChangeList(moved, changed);
            }
        }

        return counts;
    }

    private static bool IsSameCall(MemoryHistoryWrite write, MemoryHistoryWrite other)
        => write.CallNodeIndex >= 0 && write.CallNodeIndex == other.CallNodeIndex && write.CallIndex == other.CallIndex;

    private readonly record struct Reference(int Block, int Offset, MemoryHistoryGraphTarget Target);

    private readonly record struct SlotSegment(MemoryHistoryGraphTarget Target, int Owner, TracePosition Origin);

    private sealed record SlotChangeList(Dictionary<TracePosition, MemoryHistoryWrite> References, List<MemoryHistoryWrite> Data);

    private readonly record struct TargetOffset(TracePosition From, TracePosition To, int Offset);
}
