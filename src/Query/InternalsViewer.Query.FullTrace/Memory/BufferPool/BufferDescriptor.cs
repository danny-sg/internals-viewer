using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public static class BufferDescriptor
{
    public const int Size = 0xC0;

    public const int FrameOffset = 0x00;

    public const int DatabaseIdOffset = 0x2C;

    public const int StateHistoryOffset = 0x30;

    public const int ReadTimeOffset = 0x40;

    public const int ErrorCodeOffset = 0x5C;

    public const int LastUsedOffset = 0x60;

    public const int StatusOffset = 0x64;

    public const int PageIdOffset = 0x70;

    public const int FileIdOffset = 0x74;

    public const int PartitionOffset = 0x76;

    public const int RecoveryUnitOffset = 0x90;

    public const int LatchWaitersOffset = 0x98;

    public const int LatchOwnerOffset = 0xA0;

    public const int LatchStateOffset = 0xA8;

    private const ulong ExclusiveFlag = 0x02;

    private const ulong WaitersFlag = 0x08;

    private const int SharedShift = 12;

    public static List<BufferField> Fields { get; } =
    [
        new("Frame (bpage)", FrameOffset, sizeof(ulong)),
        new("Database Id (bdbid)", DatabaseIdOffset, sizeof(ushort)),
        new("State History (blog)", StateHistoryOffset, sizeof(uint)),
        new("Read \u00B5s (bReadMicroSec)", ReadTimeOffset, sizeof(ulong)),
        new("Error Code (berrcode)", ErrorCodeOffset, sizeof(uint)),
        new("Last Used (bUse1)", LastUsedOffset, sizeof(ushort)),
        new("Status (bstat)", StatusOffset, sizeof(uint)),
        new("Page ID (bpageno)", PageIdOffset, sizeof(uint)),
        new("File ID (bpageno)", FileIdOffset, sizeof(ushort)),
        new("Partition (bpart)", PartitionOffset, sizeof(ushort)),
        new("Recovery Unit (bpru)", RecoveryUnitOffset, sizeof(ulong)),
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
