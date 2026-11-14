using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Address;

internal static class ObjectClassResolver
{
    private const int PointerSize = sizeof(ulong);

    private const int AllBytes = 0xFF;

    public static AddressMap Resolve(AddressMap map, MemoryHistoryStore store, IReadOnlyDictionary<ulong, string> classes)
    {
        var polymorphic = classes.Values.ToHashSet();

        var known = new Dictionary<int, bool>();

        var uses = new Dictionary<int, List<TracePosition>>();

        foreach (var entry in map.GetEntries())
        {
            if (GetBlock(store, entry) is not (>= 0 and var block))
            {
                continue;
            }

            if (IsKnownClass(entry))
            {
                known[block] = known.GetValueOrDefault(block) || polymorphic.Contains(entry.ClassName!);
            }

            if (entry.Operation is AddressMapOperation.Visited or AddressMapOperation.Declared)
            {
                if (!uses.TryGetValue(block, out var positions))
                {
                    uses[block] = positions = [];
                }

                positions.Add(entry.FromPosition);
            }
        }

        var resolved = new Dictionary<int, string?>();

        return map.WithVtableClasses(e => GetEntryClass(store, classes, known, uses, resolved, e) ?? e.VtableClassName);
    }

    public static bool IsKnownClass(AddressMapEntry entry)
        => entry is { Operation: AddressMapOperation.Constructed or AddressMapOperation.Visited, ClassName: not null };

    public static ObjectClass GetClass(MemoryHistoryStore store,
                                       IReadOnlyDictionary<ulong, string> classes,
                                       int block,
                                       IReadOnlyList<TracePosition> uses)
    {
        var timeline = GetTimeline(store, classes, block);

        var vtableCount = timeline.Where(t => t.ClassName is not null).Select(t => t.Vtable).Distinct().Count();

        if (vtableCount == 0)
        {
            return default;
        }

        var className = GetClassInUse(timeline, uses) ?? GetMostDerived(timeline);

        var isLeftAtFree = store.Blocks[block].ToPosition.IsKnown && timeline.FindLast(t => t.ClassName is not null).ClassName == className;

        return new ObjectClass(className, vtableCount, isLeftAtFree);
    }

    private static List<VtableWrite> GetTimeline(MemoryHistoryStore store, IReadOnlyDictionary<ulong, string> classes, int block)
    {
        var timeline = new List<VtableWrite>();

        if (store.Blocks[block].Size < PointerSize)
        {
            return timeline;
        }

        Span<byte> value = stackalloc byte[PointerSize];

        var known = 0;

        foreach (var write in store.GetWrites(block))
        {
            if (write.Type == MemoryHistoryAccessType.Read || write.Offset >= PointerSize || write.Offset + write.Length <= 0)
            {
                continue;
            }

            var first = Math.Max(write.Offset, 0);

            var last = Math.Min(write.Offset + write.Length, PointerSize);

            store.CopyBytes(write, first - write.Offset, value[first..last]);

            known |= (AllBytes >> (PointerSize - (last - first))) << first;

            var pointer = BinaryPrimitives.ReadUInt64LittleEndian(value);

            var className = known == AllBytes ? classes.GetValueOrDefault(pointer) : null;

            timeline.Add(new VtableWrite(write.Position, pointer, className));
        }

        return timeline;
    }

    private static string? GetClassInUse(List<VtableWrite> timeline, IReadOnlyList<TracePosition> uses)
    {
        var counts = new Dictionary<string, int>();

        foreach (var use in uses)
        {
            var index = timeline.FindLastIndex(t => t.Position <= use);

            if (index >= 0 && timeline[index].ClassName is { } className)
            {
                counts[className] = counts.GetValueOrDefault(className) + 1;
            }
        }

        return counts.Count == 0 ? null : counts.MaxBy(c => c.Value).Key;
    }

    private static string? GetMostDerived(List<VtableWrite> timeline)
    {
        var classes = new List<string>();

        foreach (var write in timeline)
        {
            if (write.ClassName is { } className && (classes.Count == 0 || classes[^1] != className))
            {
                classes.Add(className);
            }
        }

        var seen = new HashSet<string> { classes[0] };

        for (var index = 1; index < classes.Count; index++)
        {
            if (!seen.Add(classes[index]))
            {
                return classes[index - 1];
            }
        }

        return classes[^1];
    }

    private static int GetBlock(MemoryHistoryStore store, AddressMapEntry entry)
        => store.GetBlock(entry.Address, entry.FromPosition) is >= 0 and var block && store.Blocks[block].Address == entry.Address
            ? block
            : -1;

    private static string? GetEntryClass(MemoryHistoryStore store,
                                         IReadOnlyDictionary<ulong, string> classes,
                                         Dictionary<int, bool> known,
                                         Dictionary<int, List<TracePosition>> uses,
                                         Dictionary<int, string?> resolved,
                                         AddressMapEntry entry)
    {
        if (GetBlock(store, entry) is not (>= 0 and var block))
        {
            return null;
        }

        if (!resolved.TryGetValue(block, out var className))
        {
            var found = GetClass(store, classes, block, uses.GetValueOrDefault(block) ?? []);

            var isKnown = known.TryGetValue(block, out var isPolymorphic);

            var isDoubtful = !isPolymorphic || found.VtableCount < 2 || found.IsLeftAtFree;

            className = isKnown && isDoubtful ? null : found.ClassName;

            resolved[block] = className;
        }

        return className;
    }

    private readonly record struct VtableWrite(TracePosition Position, ulong Vtable, string? ClassName);

    internal readonly record struct ObjectClass(string? ClassName, int VtableCount, bool IsLeftAtFree);
}
