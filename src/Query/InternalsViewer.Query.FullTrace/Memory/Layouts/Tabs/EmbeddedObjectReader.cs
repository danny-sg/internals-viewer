using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;

public static class EmbeddedObjectReader
{
    private const int PointerSize = sizeof(ulong);

    public static List<EmbeddedObject> Read(AddressMap map,
                                            MemoryHistoryStore store,
                                            TraceCallStore? calls,
                                            IReadOnlyDictionary<ulong, string> vtables,
                                            int block,
                                            TracePosition position)
    {
        var item = store.Blocks[block];

        var found = new SortedDictionary<ulong, string>();

        foreach (var entry in map.GetEmbedded(item.Address, item.Size, item.FromPosition, position))
        {
            found[entry.Address] = (entry.VtableClassName ?? entry.ClassName)!;
        }

        var baseParts = new HashSet<ulong>();

        foreach (var embedded in FindVtableObjects(store, vtables, block, position, baseParts))
        {
            found[embedded.Address] = embedded.Type;
        }

        foreach (var basePart in baseParts)
        {
            found.Remove(basePart);
        }

        var classNames = new Dictionary<int, string?>();

        return Measure([.. found.Select(f => new EmbeddedObject(f.Key, f.Value, 0))],
                       store,
                       block,
                       n => GetClassName(calls, classNames, n));
    }

    internal static List<EmbeddedObject> FindVtableObjects(MemoryHistoryStore store,
                                                           IReadOnlyDictionary<ulong, string> vtables,
                                                           int block,
                                                           TracePosition position,
                                                           HashSet<ulong>? baseParts = null)
    {
        var objects = new List<EmbeddedObject>();

        if (vtables.Count == 0 || store.GetWriteCount(block, position) is not (> 0 and var count))
        {
            return objects;
        }

        var address = store.Blocks[block].Address;

        var image = store.GetImage(block, count - 1);

        var started = new Dictionary<string, ulong>();

        for (var offset = (int)((PointerSize - address % PointerSize) % PointerSize);
             offset + PointerSize <= image.Data.Length;
             offset += PointerSize)
        {
            if (image.Known.AsSpan(offset, PointerSize).Contains(false))
            {
                continue;
            }

            var vtable = BinaryPrimitives.ReadUInt64LittleEndian(image.Data.AsSpan(offset));

            if (!vtables.TryGetValue(vtable, out var className))
            {
                continue;
            }

            if (started.TryGetValue(className, out var first) && first != vtable)
            {
                baseParts?.Add(address + (ulong)offset);

                continue;
            }

            started[className] = vtable;

            if (offset > 0)
            {
                objects.Add(new EmbeddedObject(address + (ulong)offset, className, 0));
            }
        }

        return objects;
    }

    internal static List<EmbeddedObject> Measure(List<EmbeddedObject> objects,
                                                 MemoryHistoryStore store,
                                                 int block,
                                                 Func<int, string?> getClassName)
    {
        if (objects.Count == 0)
        {
            return [];
        }

        var address = store.Blocks[block].Address;

        var size = store.Blocks[block].Size;

        int[] starts = [.. objects.Select(o => (int)(o.Address - address))];

        var ends = new int[objects.Count];

        foreach (var write in store.GetWrites(block))
        {
            Add(write.ReplayNodeIndex, write.Offset, write.Length);
        }

        foreach (var read in store.GetReads(block))
        {
            Add(read.ReplayNodeIndex, read.Offset, read.Length);
        }

        return [.. objects.Select((o, i) => o with { Length = Math.Max(0, ends[i] - starts[i]) })];

        void Add(int replayNodeIndex, int offset, int length)
        {
            var index = SortedSearch.FirstAfter<int>(starts, offset) - 1;

            if (index < 0)
            {
                return;
            }

            var limit = index + 1 < starts.Length ? starts[index + 1] : size;

            if (offset < limit && getClassName(replayNodeIndex) == objects[index].Type)
            {
                ends[index] = Math.Max(ends[index], Math.Min(offset + length, limit));
            }
        }
    }

    private static string? GetClassName(TraceCallStore? calls, Dictionary<int, string?> classNames, int replayNodeIndex)
    {
        if (!classNames.TryGetValue(replayNodeIndex, out var className))
        {
            className = calls?.GetNode(replayNodeIndex)?.Frame?.Function?.ClassName is { Length: > 0 } found ? found : null;

            classNames[replayNodeIndex] = className;
        }

        return className;
    }
}
