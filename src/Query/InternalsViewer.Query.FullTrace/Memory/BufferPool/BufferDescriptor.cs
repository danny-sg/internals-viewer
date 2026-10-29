using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public static class BufferDescriptor
{
    public const int Size = 0xC0;

    public const int FrameOffset = 0x00;

    public const int DatabaseIdOffset = 0x2C;

    public const int PageIdOffset = 0x70;

    public const int FileIdOffset = 0x74;

    public const int LatchWaitersOffset = 0x98;

    public const int LatchOwnerOffset = 0xA0;

    public const int LatchStateOffset = 0xA8;

    private const ulong ExclusiveFlag = 0x02;

    private const ulong WaitersFlag = 0x08;

    private const int SharedShift = 12;

    public static List<BufferField> Fields { get; } =
    [
        new("Frame", FrameOffset, sizeof(ulong)),
        new("Database ID", DatabaseIdOffset, sizeof(ushort)),
        new("Page ID", PageIdOffset, sizeof(uint)),
        new("File ID", FileIdOffset, sizeof(ushort)),
        new("Latch Waiters", LatchWaitersOffset, sizeof(ulong)),
        new("Latch Owner", LatchOwnerOffset, sizeof(ulong)),
        new("Latch State", LatchStateOffset, sizeof(ulong))
    ];

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

        Span<byte> word = stackalloc byte[sizeof(ulong)];

        for (var index = 0; index < writes.Count; index++)
        {
            var write = writes[index];

            var start = Math.Max(write.Offset, LatchStateOffset);

            var end = Math.Min(write.Offset + write.Length, LatchStateOffset + sizeof(ulong));

            if (start >= end)
            {
                continue;
            }

            store.GetBytes(write)[(start - write.Offset)..(end - write.Offset)].CopyTo(word[(start - LatchStateOffset)..]);

            states.Add((first + index, BinaryPrimitives.ReadUInt64LittleEndian(word)));
        }

        return states;
    }
}
