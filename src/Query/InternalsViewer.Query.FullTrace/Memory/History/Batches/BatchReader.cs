using System.Runtime.InteropServices;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Memory.History.Batches.Enums;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Memory.History.Batches;

public static class BatchReader
{
    public const string ClassName = "CBpBatch";

    public const int VectorEntrySize = 0x58;

    private const ulong AllocationBitmapOffset = 0x00;

    private const ulong VectorArrayOffset = 0x08;

    private const ulong PureBitmapOffset = 0x10;

    private const ulong VectorCapacityOffset = 0x18;

    private const ulong VectorCountOffset = 0x1C;

    private const ulong VersionOffset = 0x20;

    private const ulong CapacityOffset = 0x24;

    private const ulong ActiveRowCountOffset = 0x28;

    private const ulong MemoryObjectOffset = 0x30;

    private const ulong SelectionOffset = 0x38;

    private const ulong DeepDataAllocatorOffset = 0x40;

    private const ulong FreeDeepPagesOffset = 0x48;

    private const ulong BlobHandleFactoryOffset = 0x58;

    private const ulong CurrentDeepPageOffset = 0x10;

    private const ulong FirstDeepPageOffset = 0x18;

    private const int MaximumVectors = 1024;

    private const int MaximumRows = 0x10000;

    public static List<BatchField> Fields { get; } =
    [
        new("Allocation Bitmap", (int)AllocationBitmapOffset, sizeof(ulong)),
        new("Vector Array", (int)VectorArrayOffset, sizeof(ulong)),
        new("Pure Bitmap", (int)PureBitmapOffset, sizeof(ulong)),
        new("Vector Capacity", (int)VectorCapacityOffset, sizeof(int)),
        new("Vector Count", (int)VectorCountOffset, sizeof(int)),
        new("Version", (int)VersionOffset, sizeof(int), IsHex: true),
        new("Capacity", (int)CapacityOffset, sizeof(int)),
        new("Active Rows", (int)ActiveRowCountOffset, sizeof(int)),
        new("Memory Object", (int)MemoryObjectOffset, sizeof(ulong)),
        new("Selection Vector", (int)SelectionOffset, sizeof(ulong)),
        new("Deep Data Allocator", (int)DeepDataAllocatorOffset, sizeof(ulong)),
        new("Free Deep Pages", (int)FreeDeepPagesOffset, sizeof(ulong)),
        new("Blob Handle Factory", (int)BlobHandleFactoryOffset, sizeof(ulong))
    ];

    public static BatchState? Read(IMemoryReader memory, ulong address)
    {
        if (ReadHeader(memory, address) is not { } header)
        {
            return null;
        }

        var selection = ReadSelection(memory, header.SelectionAddress, Math.Clamp(header.ActiveRowCount, 0, MaximumRows));

        var rowCount = selection.Length > 0 ? selection.Max() + 1 : 0;

        var vectors = new List<BatchVector>();

        for (var index = 0; index < header.VectorCount; index++)
        {
            var isPure = IsPure(memory, header.PureBitmapAddress, index);

            var vector = ReadVector(memory, index + 1, GetBuffer(memory, header, index), isPure, isPure ? 1 : rowCount);

            vectors.Add(vector with
            {
                CurrentDeepPage = GetEntryPointer(memory, header, index, CurrentDeepPageOffset),
                FirstDeepPage = GetEntryPointer(memory, header, index, FirstDeepPageOffset)
            });
        }

        return new BatchState(address,
                              header.Capacity,
                              header.ActiveRowCount,
                              header.VectorArrayAddress,
                              header.AllocationBitmapAddress,
                              header.PureBitmapAddress,
                              header.SelectionAddress,
                              selection,
                              rowCount,
                              vectors);
    }

    public static List<(ulong Address, BatchPart Part)> GetParts(IMemoryReader memory, ulong address)
    {
        if (ReadHeader(memory, address) is not { } header)
        {
            return [];
        }

        List<(ulong Address, BatchPart Part)> parts =
        [
            (header.VectorArrayAddress, new BatchPart(BatchPartType.VectorArray)),
            (header.AllocationBitmapAddress, new BatchPart(BatchPartType.AllocationBitmap)),
            (header.PureBitmapAddress, new BatchPart(BatchPartType.PureBitmap)),
            (header.SelectionAddress, new BatchPart(BatchPartType.Selection))
        ];

        for (var index = 0; index < header.VectorCount; index++)
        {
            parts.Add((GetBuffer(memory, header, index), new BatchPart(BatchPartType.Vector, index + 1)));

            var deepData = new BatchPart(BatchPartType.DeepData, index + 1);

            parts.Add((GetEntryPointer(memory, header, index, CurrentDeepPageOffset), deepData));

            var firstPage = GetEntryPointer(memory, header, index, FirstDeepPageOffset);

            parts.AddRange(DeepDataReader.GetPages(memory, firstPage).Select(p => (p, deepData)));
        }

        return parts;
    }

    private static Header? ReadHeader(IMemoryReader memory, ulong address)
    {
        if (!memory.TryRead(address + VectorArrayOffset, out ulong vectorArray)
            || !memory.TryRead(address + VectorCountOffset, out int vectorCount)
            || !memory.TryRead(address + CapacityOffset, out int capacity)
            || !memory.TryRead(address + ActiveRowCountOffset, out int activeRowCount))
        {
            return null;
        }

        memory.TryRead(address + AllocationBitmapOffset, out ulong allocationBitmap);

        memory.TryRead(address + PureBitmapOffset, out ulong pureBitmap);

        memory.TryRead(address + SelectionOffset, out ulong selection);

        return new Header(vectorArray,
                          Math.Clamp(vectorCount, 0, MaximumVectors),
                          capacity,
                          activeRowCount,
                          allocationBitmap,
                          pureBitmap,
                          selection);
    }

    private static ulong GetBuffer(IMemoryReader memory, Header header, int index) => GetEntryPointer(memory, header, index, 0);

    private static ulong GetEntryPointer(IMemoryReader memory, Header header, int index, ulong offset)
        => header.VectorArrayAddress != 0
           && memory.TryRead(header.VectorArrayAddress + (ulong)(index * VectorEntrySize) + offset, out ulong pointer)
            ? pointer
            : 0;

    private static ushort[] ReadSelection(IMemoryReader memory, ulong address, int count)
    {
        if (address == 0 || count == 0)
        {
            return [];
        }

        var selection = new ushort[count];

        var read = memory.Read(address, MemoryMarshal.AsBytes(selection.AsSpan())) / sizeof(ushort);

        return read == count ? selection : selection[..read];
    }

    private static BatchVector ReadVector(IMemoryReader memory, int id, ulong buffer, bool isPure, int slotCount)
    {
        var count = buffer == 0 ? 0 : slotCount;

        var slots = new ulong[count];

        var known = new bool[count];

        for (var index = 0; index < count; index++)
        {
            known[index] = memory.TryRead(buffer + (ulong)index * sizeof(ulong), out slots[index]);
        }

        return new BatchVector(id, buffer, isPure, slots, known);
    }

    private static bool IsPure(IMemoryReader memory, ulong bitmap, int index)
        => bitmap != 0 && memory.TryRead(bitmap + (ulong)(index / 8), out byte bits) && (bits & (1 << (index % 8))) != 0;

    private readonly record struct Header(ulong VectorArrayAddress,
                                          int VectorCount,
                                          int Capacity,
                                          int ActiveRowCount,
                                          ulong AllocationBitmapAddress,
                                          ulong PureBitmapAddress,
                                          ulong SelectionAddress);
}
