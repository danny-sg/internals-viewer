using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public static class MemoryPointers
{
    public const int PointerSize = sizeof(ulong);

    public static List<MemoryPointer> Read(MemoryHistoryViewModel history, int block, TracePosition position)
    {
        var store = history.Store;

        var count = store.IsLive(block, position) ? store.GetWriteCount(block, position) : 0;

        return count > 0 ? Find(history, store.GetImage(block, count - 1), store.Blocks[block].Address, position) : [];
    }

    public static List<MemoryPointer> Find(MemoryHistoryViewModel history, MemoryHistoryImage image, ulong address, TracePosition position)
    {
        var pointers = new List<MemoryPointer>();

        for (var offset = (int)((PointerSize - address % PointerSize) % PointerSize);
             offset + PointerSize <= image.Data.Length;
             offset += PointerSize)
        {
            if (image.Known.AsSpan(offset, PointerSize).Contains(false))
            {
                continue;
            }

            var value = BinaryPrimitives.ReadUInt64LittleEndian(image.Data.AsSpan(offset));

            if (value == 0)
            {
                continue;
            }

            if (history.Store.GetBlock(value, position) is >= 0 and var target)
            {
                pointers.Add(new MemoryPointer(offset, value, target, null));
            }
            else if (history.GetBufferPage(value, position) is { } page)
            {
                pointers.Add(new MemoryPointer(offset, value, -1, page));
            }
        }

        return pointers;
    }
}
