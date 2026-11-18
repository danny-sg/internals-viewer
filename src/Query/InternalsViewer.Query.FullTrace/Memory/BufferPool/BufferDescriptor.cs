using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public static class BufferDescriptor
{
    public static BufferLatchState GetLatchState(EngineLayout.BufferLayout layout, ulong state)
    {
        if (HasWaiters(layout, state))
        {
            return BufferLatchState.Waiting;
        }

        if (IsExclusive(layout, state))
        {
            return BufferLatchState.Exclusive;
        }

        return GetSharedCount(layout, state) > 0 ? BufferLatchState.Shared : BufferLatchState.Released;
    }

    public static int GetSharedCount(EngineLayout.BufferLayout layout, ulong state) => (int)(state >> layout.LatchSharedShift);

    public static bool IsExclusive(EngineLayout.BufferLayout layout, ulong state) => (state & layout.LatchExclusiveFlag) != 0;

    public static bool HasWaiters(EngineLayout.BufferLayout layout, ulong state) => (state & layout.LatchWaitersFlag) != 0;

    public static List<(int Write, BufferLatchState State)> GetLatchStates(MemoryHistoryStore store, int block)
    {
        var states = new List<(int Write, BufferLatchState State)>();

        var writes = store.GetWrites(block);

        var first = store.Blocks[block].FirstWrite;

        var offset = store.Layout.Buffer.LatchStateField.Offset;

        Span<byte> word = stackalloc byte[sizeof(ulong)];

        for (var index = 0; index < writes.Count; index++)
        {
            var write = writes[index];

            var start = Math.Max(write.Offset, offset);

            var end = Math.Min(write.Offset + write.Length, offset + sizeof(ulong));

            if (start >= end)
            {
                continue;
            }

            store.CopyBytes(write, start - write.Offset, word[(start - offset)..(end - offset)]);

            states.Add((first + index, GetLatchState(store.Layout.Buffer, BinaryPrimitives.ReadUInt64LittleEndian(word))));
        }

        return states;
    }
}
