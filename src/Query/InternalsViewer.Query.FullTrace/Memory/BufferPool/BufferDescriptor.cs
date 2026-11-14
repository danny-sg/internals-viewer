using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public static class BufferDescriptor
{
    private const ulong ExclusiveFlag = 0x02;

    private const ulong WaitersFlag = 0x08;

    private const int SharedShift = 12;

    public static BufferLatchState GetLatchState(ulong state)
    {
        if (HasWaiters(state))
        {
            return BufferLatchState.Waiting;
        }

        if (IsExclusive(state))
        {
            return BufferLatchState.Exclusive;
        }

        return GetSharedCount(state) > 0 ? BufferLatchState.Shared : BufferLatchState.Released;
    }

    public static int GetSharedCount(ulong state) => (int)(state >> SharedShift);

    public static bool IsExclusive(ulong state) => (state & ExclusiveFlag) != 0;

    public static bool HasWaiters(ulong state) => (state & WaitersFlag) != 0;

    public static List<(int Write, ulong State)> GetLatchStates(MemoryHistoryStore store, int block)
    {
        var states = new List<(int Write, ulong State)>();

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

            states.Add((first + index, BinaryPrimitives.ReadUInt64LittleEndian(word)));
        }

        return states;
    }
}
