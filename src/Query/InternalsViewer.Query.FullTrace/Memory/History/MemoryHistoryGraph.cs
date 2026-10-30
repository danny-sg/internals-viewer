using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed class MemoryHistoryGraph
{
    private const int PointerSize = MemoryHistoryPointers.PointerSize;

    private MemoryHistoryGraph(List<MemoryHistoryGraphNode> nodes, List<MemoryHistoryGraphEdge> edges, bool isTruncated)
    {
        Nodes = nodes;

        Edges = edges;

        IsTruncated = isTruncated;
    }

    public static MemoryHistoryGraph Empty { get; } = new([], [], false);

    public List<MemoryHistoryGraphNode> Nodes { get; }

    public List<MemoryHistoryGraphEdge> Edges { get; }

    public bool IsTruncated { get; }

    public static MemoryHistoryGraph Build(MemoryHistoryStore store,
                                           TraceBufferPool pool,
                                           int block,
                                           TracePosition from,
                                           TracePosition to,
                                           TracePosition shutdown,
                                           Func<string, bool> isExpanded,
                                           Func<string, bool> isReferrersExpanded,
                                           int depth,
                                           int limit)
    {
        var root = store.Blocks[block];

        MemoryHistoryGraphTarget[] rootTargets = [new(root.FromPosition, root.ToPosition, root.Address, block, null)];

        var changesEnd = shutdown < to ? shutdown : to;

        var changes = new Dictionary<int, Dictionary<int, SlotChanges>>();

        var nodes = new List<MemoryHistoryGraphNode>
        {
            new(string.Empty, 0, -1, rootTargets, GetDataMarks(store, pool, rootTargets, from, changesEnd, changes))
        };

        var edges = new List<MemoryHistoryGraphEdge>();

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

            foreach (var (offset, segments) in GetSlots(store, pool, node, from, to, timelines))
            {
                List<MemoryHistoryGraphTarget> targets = [.. segments.Select(s => s.Target)];

                var moves = GetMoves(store, pool, segments, offset, from, changesEnd, changes);

                var changeCount = moves.Count;

                if (targets.All(t => owners.ContainsKey(GetTargetKey(t))))
                {
                    foreach (var owned in targets.GroupBy(t => owners[GetTargetKey(t)]))
                    {
                        edges.Add(new MemoryHistoryGraphEdge(index, owned.Key, offset, GetTargetOffset(store, [.. owned]), changeCount));
                    }

                    continue;
                }

                if (nodes.Count >= limit)
                {
                    isTruncated = true;

                    continue;
                }

                var target = nodes.Count;

                nodes.Add(new MemoryHistoryGraphNode($"{node.Path}/{offset:X}",
                                                     node.Depth + 1,
                                                     index,
                                                     [.. targets],
                                                     [.. moves, .. GetDataMarks(store, pool, targets, from, changesEnd, changes)]));

                Own(owners, targets, target);

                queue.Enqueue(target);

                edges.Add(new MemoryHistoryGraphEdge(index, target, offset, GetTargetOffset(store, targets), changeCount));
            }
        }

        var placed = nodes.SelectMany(n => GetBlocks(n.Targets)).ToHashSet();

        var level = isReferrersExpanded(string.Empty) ? new List<int> { 0 } : [];

        while (level.Count > 0)
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

                    var path = $"{node.Path}<{key}";

                    var targets = GetReferrerTargets(store, lane);

                    nodes.Add(new MemoryHistoryGraphNode(path,
                                                         node.Depth - 1,
                                                         index,
                                                         targets,
                                                         GetDataMarks(store, pool, targets, from, changesEnd, changes)));

                    placed.UnionWith(lane.Select(r => r.Block));

                    foreach (var slot in lane.GroupBy(r => r.Offset).OrderBy(g => g.Key))
                    {
                        List<SlotSegment> pointers = [.. slot.Select(r => new SlotSegment(r.Target, r.Block, r.Target.From))];

                        edges.Add(new MemoryHistoryGraphEdge(referrer,
                                                             index,
                                                             slot.Key,
                                                             GetTargetOffset(store, [.. slot.Select(r => r.Target)]),
                                                             GetMoves(store, pool, pointers, slot.Key, from, changesEnd, changes).Count));
                    }

                    if (1 - node.Depth < depth && isReferrersExpanded(path))
                    {
                        next.Add(referrer);
                    }
                }
            }

            level = next;
        }

        foreach (var node in nodes.Where(n => !n.IsReferrer))
        {
            node.HasPointers = GetSlots(store, pool, node, from, to, timelines).Count > 0;
        }

        foreach (var node in nodes.Where(n => n.Targets.Length > 0 && n.Targets.All(t => t.Page is null && t.Block == n.Targets[0].Block)))
        {
            var info = store.Blocks[node.Targets[0].Block];

            node.Object = new MemoryHistoryGraphTarget(info.FromPosition, info.ToPosition, info.Address, node.Targets[0].Block, null);
        }

        var unplaced = GetReferences(store, pool, [.. nodes.Where(n => n.Depth <= 0).SelectMany(n => GetBlocks(n.Targets))], from, to)
                       .Where(r => !placed.Contains(r.Block))
                       .Select(r => r.Target.Block)
                       .ToHashSet();

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];

            if (node.Depth <= 0)
            {
                node.HasReferrers = nodes.Any(n => n.IsReferrer && n.Parent == index) || GetBlocks(node.Targets).Any(unplaced.Contains);
            }
        }

        return new MemoryHistoryGraph(nodes, edges, isTruncated);
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
                    Object = node.Object
                });
            }
        }

        var edges = Edges.Where(e => isKept[e.From] && isKept[e.To] && IsTreeEdge(e))
                         .Select(e => e with { From = indexes[e.From], To = indexes[e.To] })
                         .ToList();

        return new MemoryHistoryGraph(nodes, edges, IsTruncated);
    }

    public bool IsTreeEdge(MemoryHistoryGraphEdge edge)
        => edge.From != edge.To
           && (Nodes[edge.To].Parent == edge.From || (Nodes[edge.From].IsReferrer && Nodes[edge.From].Parent == edge.To));

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

    private static IEnumerable<int> GetBlocks(IEnumerable<MemoryHistoryGraphTarget> targets)
        => targets.Where(t => t.Block >= 0).Select(t => t.Block).Distinct();

    private static List<Reference> GetReferences(MemoryHistoryStore store,
                                                 TraceBufferPool pool,
                                                 HashSet<int> blocks,
                                                 TracePosition from,
                                                 TracePosition to)
    {
        var ranges = blocks.Select(b => (Start: store.Blocks[b].Address, End: store.Blocks[b].Address + (ulong)store.Blocks[b].Size))
                           .ToArray();

        var references = new List<Reference>();

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            var info = store.Blocks[block];

            if (blocks.Contains(block) || info.WriteCount == 0 || info.FromPosition > to || info.ToPosition <= from)
            {
                continue;
            }

            var timeline = GetTimeline(store, pool, block, from, to, v => ranges.Any(r => v >= r.Start && v < r.End));

            foreach (var (offset, segments) in timeline)
            {
                references.AddRange(segments.Where(s => blocks.Contains(s.Block)).Select(s => new Reference(block, offset, s)));
            }
        }

        return references;
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

            var spans = referrer.Select(r => r.Target).OrderBy(t => t.From).ToList();

            var start = spans[0].From;

            var end = spans[0].To;

            foreach (var span in spans.Skip(1))
            {
                if (span.From <= end)
                {
                    end = span.To > end ? span.To : end;

                    continue;
                }

                targets.Add(new MemoryHistoryGraphTarget(start, end, address, referrer.Key, null));

                start = span.From;

                end = span.To;
            }

            targets.Add(new MemoryHistoryGraphTarget(start, end, address, referrer.Key, null));
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

            Apply(store, write, data, known);

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

            Apply(store, write, data, known);

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

    private static void Apply(MemoryHistoryStore store, MemoryHistoryWrite write, byte[] data, bool[] known)
    {
        store.GetBytes(write).CopyTo(data.AsSpan(write.Offset));

        known.AsSpan(write.Offset, write.Length).Fill(true);
    }

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
            || MemoryHistoryPointers.GetTarget(store, pool, value, position) is not { } target
            || target.Block == block)
        {
            return;
        }

        var end = target.Page is { } page
            ? pool.GetReferenceEnd(page, value, position)
            : store.Blocks[target.Block].ToPosition;

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

    private static int? GetTargetOffset(MemoryHistoryStore store, List<MemoryHistoryGraphTarget> targets)
    {
        int? found = null;

        foreach (var target in targets)
        {
            var offset = target.Page is { } page ? page.Offset : (int)(target.Value - store.Blocks[target.Block].Address);

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
                                                         Dictionary<int, Dictionary<int, SlotChanges>> changes)
    {
        var moves = new List<MemoryHistoryGraphMark>();

        var counted = new HashSet<(int Owner, TracePosition Origin)>();

        ulong? previous = null;

        foreach (var segment in segments.OrderBy(s => s.Target.From))
        {
            if (previous is { } last
                && last != segment.Target.Value
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
                                                             Dictionary<int, Dictionary<int, SlotChanges>> changes)
    {
        var marks = new List<MemoryHistoryGraphMark>();

        foreach (var block in GetBlocks(targets))
        {
            var writes = GetChangeCounts(store, pool, block, from, to, changes).Values.SelectMany(s => s.Data).DistinctBy(w => w.Position);

            marks.AddRange(writes.Select(w => new MemoryHistoryGraphMark(w.Position, w.CallNodeIndex, false)));
        }

        return marks;
    }

    private static Dictionary<int, SlotChanges> GetChangeCounts(MemoryHistoryStore store,
                                                                TraceBufferPool pool,
                                                                int block,
                                                                TracePosition from,
                                                                TracePosition to,
                                                                Dictionary<int, Dictionary<int, SlotChanges>> changes)
    {
        if (!changes.TryGetValue(block, out var counts))
        {
            counts = CountChanges(store, pool, block, from, to);

            changes[block] = counts;
        }

        return counts;
    }

    private static Dictionary<int, SlotChanges> CountChanges(MemoryHistoryStore store,
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
            Apply(store, write, data, known);

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
                    ? MemoryHistoryPointers.GetTarget(store, pool, pointer, write.Position)
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

        var counts = new Dictionary<int, SlotChanges>();

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
                counts[first + slot * PointerSize] = new SlotChanges(moved, changed);
            }
        }

        return counts;
    }

    private static bool IsSameCall(MemoryHistoryWrite write, MemoryHistoryWrite other)
        => write.CallNodeIndex >= 0 && write.CallNodeIndex == other.CallNodeIndex && write.CallIndex == other.CallIndex;

    private readonly record struct Reference(int Block, int Offset, MemoryHistoryGraphTarget Target);

    private readonly record struct SlotSegment(MemoryHistoryGraphTarget Target, int Owner, TracePosition Origin);

    private sealed record SlotChanges(Dictionary<TracePosition, MemoryHistoryWrite> References, List<MemoryHistoryWrite> Data);
}
