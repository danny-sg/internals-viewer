using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public static class MemoryHistoryPointerReader
{
    public const int PointerSize = sizeof(ulong);

    public static List<MemoryHistoryPointer> Read(MemoryHistoryStore store,
                                                  TraceBufferPool pool,
                                                  int block,
                                                  ulong address,
                                                  TracePosition position)
    {
        var count = store.IsLive(block, position) ? store.GetWriteCount(block, position) : 0;

        if (count <= 0)
        {
            return [];
        }

        var skip = (int)(address - store.Blocks[block].Address);

        return
        [
            .. Find(store, pool, block, store.GetImage(block, count - 1), count, position)
               .Where(p => p.Offset >= skip)
               .Select(p => p with { Offset = p.Offset - skip })
        ];
    }

    public static List<MemoryHistoryPointer>? ReadArray(MemoryHistoryStore store,
                                                        TraceBufferPool pool,
                                                        ulong address,
                                                        TracePosition position)
    {
        if (store.GetBlock(address, position) is not (>= 0 and var block)
            || store.Blocks[block] is not { Size: > 0 } item
            || item.Address != address
            || address % PointerSize != 0
            || item.Size % PointerSize != 0
            || store.GetWriteCount(block, position) is not (> 0 and var count))
        {
            return null;
        }

        var image = store.GetImage(block, count - 1);

        var pointers = Find(store, pool, block, image, count, position);

        var next = 0;

        for (var offset = 0; offset < item.Size; offset += PointerSize)
        {
            if (next < pointers.Count && pointers[next].Offset == offset)
            {
                next++;
            }
            else if (!IsEmpty(image, offset))
            {
                return null;
            }
        }

        return pointers.Count > 0 ? pointers : null;
    }

    public static (List<MemoryHistoryPointer> Pointers, TracePosition Position)? ReadLastArray(MemoryHistoryStore store,
                                                                                               TraceBufferPool pool,
                                                                                               int block)
    {
        var writes = store.GetWrites(block);

        for (var index = writes.Count - 1; index >= 0; index--)
        {
            if (!store.GetBytes(writes[index]).AsSpan().ContainsAnyExcept((byte)0))
            {
                continue;
            }

            var position = writes[index].Position;

            return ReadArray(store, pool, store.Blocks[block].Address, position) is { } pointers ? (pointers, position) : null;
        }

        return null;
    }

    public static List<MemoryHistoryPointer> Find(MemoryHistoryStore store,
                                                  TraceBufferPool pool,
                                                  int block,
                                                  MemoryHistoryImage image,
                                                  int count,
                                                  TracePosition position)
    {
        var pointers = new List<MemoryHistoryPointer>();

        var address = store.Blocks[block].Address;

        var writes = store.GetWrites(block);

        var lastWrites = store.GetLastWrites(block, count - 1);

        for (var offset = (int)((PointerSize - address % PointerSize) % PointerSize);
             offset + PointerSize <= image.Data.Length;
             offset += PointerSize)
        {
            if (image.Known.AsSpan(offset, PointerSize).Contains(false))
            {
                continue;
            }

            var value = BinaryPrimitives.ReadUInt64LittleEndian(image.Data.AsSpan(offset));

            if (value == 0 || GetTarget(store, pool, value, position) is not { } target)
            {
                continue;
            }

            var written = writes[GetLastWrite(lastWrites, offset)].Position;

            if (GetTarget(store, pool, value, written) == target)
            {
                pointers.Add(new MemoryHistoryPointer(offset, value, target.Block, target.Page));
            }
        }

        return pointers;
    }

    public static IEnumerable<(MemoryHistoryWrite Write, ulong Value)> GetWrittenPointers(MemoryHistoryStore store, int block)
    {
        var item = store.Blocks[block];

        var writes = store.GetWrites(block);

        var data = new byte[item.Size];

        var known = new bool[item.Size];

        var first = (int)((PointerSize - item.Address % PointerSize) % PointerSize);

        for (var index = 0; index < writes.Count; index++)
        {
            var write = writes[index];

            store.Apply(write, data, known);

            var end = Math.Min(write.Offset + write.Length, data.Length - PointerSize + 1);

            for (var offset = first + Math.Max(0, (write.Offset - first) / PointerSize * PointerSize); offset < end; offset += PointerSize)
            {
                if (!known.AsSpan(offset, PointerSize).Contains(false))
                {
                    yield return (write, BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset)));
                }
            }
        }
    }

    private static bool IsEmpty(MemoryHistoryImage image, int offset)
    {
        for (var index = offset; index < offset + PointerSize; index++)
        {
            if (image.Known[index] && image.Data[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static int GetLastWrite(int[] lastWrites, int offset)
    {
        var last = lastWrites[offset];

        for (var index = offset + 1; index < offset + PointerSize; index++)
        {
            last = Math.Max(last, lastWrites[index]);
        }

        return last;
    }

    internal static (int Block, TraceBufferReference? Page)? GetTarget(MemoryHistoryStore store,
                                                                       TraceBufferPool pool,
                                                                       ulong value,
                                                                       TracePosition position)
    {
        if (store.GetBlock(value, position) is >= 0 and var block)
        {
            return (block, null);
        }

        return pool.GetReference(value, position) is { } page ? (-1, page) : null;
    }
}
