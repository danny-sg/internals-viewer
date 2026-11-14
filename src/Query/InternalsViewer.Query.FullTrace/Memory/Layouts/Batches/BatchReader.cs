using System.Runtime.InteropServices;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Batches.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Batches;

public static class BatchReader
{
    public const string ClassName = "CBpBatch";

    private const int MaximumVectors = 1024;

    private const int MaximumRows = 0x10000;

    public static BatchState? Read(ITraceMemoryReader memory, ulong address)
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
                CurrentDeepPage = GetEntryPointer(memory, header, index, memory.Layout.Batch.CurrentDeepPageField),
                FirstDeepPage = GetEntryPointer(memory, header, index, memory.Layout.Batch.FirstDeepPageField)
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
                              vectors)
        {
            VectorSize = memory.Layout.Batch.VectorSize
        };
    }

    public static List<(ulong Address, BatchPart Part)> GetParts(ITraceMemoryReader memory, ulong address)
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

            parts.Add((GetEntryPointer(memory, header, index, memory.Layout.Batch.CurrentDeepPageField), deepData));

            var firstPage = GetEntryPointer(memory, header, index, memory.Layout.Batch.FirstDeepPageField);

            parts.AddRange(DeepDataReader.GetPages(memory, firstPage).Select(p => (p, deepData)));
        }

        return parts;
    }

    private static Header? ReadHeader(ITraceMemoryReader memory, ulong address)
    {
        var layout = memory.Layout.Batch;

        if (!memory.TryRead(address + (ulong)layout.VectorArrayField.Offset, out ulong vectorArray)
            || !memory.TryRead(address + (ulong)layout.VectorCountField.Offset, out int vectorCount)
            || !memory.TryRead(address + (ulong)layout.CapacityField.Offset, out int capacity)
            || !memory.TryRead(address + (ulong)layout.ActiveRowCountField.Offset, out int activeRowCount))
        {
            return null;
        }

        memory.TryRead(address + (ulong)layout.AllocationBitmapField.Offset, out ulong allocationBitmap);

        memory.TryRead(address + (ulong)layout.PureBitmapField.Offset, out ulong pureBitmap);

        memory.TryRead(address + (ulong)layout.SelectionField.Offset, out ulong selection);

        return new Header(vectorArray,
                          Math.Clamp(vectorCount, 0, MaximumVectors),
                          capacity,
                          activeRowCount,
                          allocationBitmap,
                          pureBitmap,
                          selection);
    }

    private static ulong GetBuffer(ITraceMemoryReader memory, Header header, int index)
        => GetEntryPointer(memory, header, index, memory.Layout.Batch.VectorBufferField);

    private static ulong GetEntryPointer(ITraceMemoryReader memory, Header header, int index, MemoryField field)
        => header.VectorArrayAddress != 0
           && memory.TryRead(header.VectorArrayAddress + (ulong)(index * memory.Layout.Batch.VectorSize + field.Offset), out ulong pointer)
            ? pointer
            : 0;

    private static ushort[] ReadSelection(ITraceMemoryReader memory, ulong address, int count)
    {
        if (address == 0 || count == 0)
        {
            return [];
        }

        var selection = new ushort[count];

        var read = memory.Read(address, MemoryMarshal.AsBytes(selection.AsSpan())) / sizeof(ushort);

        return read == count ? selection : selection[..read];
    }

    private static BatchVector ReadVector(ITraceMemoryReader memory, int id, ulong buffer, bool isPure, int slotCount)
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

    private static bool IsPure(ITraceMemoryReader memory, ulong bitmap, int index)
        => bitmap != 0 && memory.TryRead(bitmap + (ulong)(index / 8), out byte bits) && (bits & (1 << (index % 8))) != 0;

    private readonly record struct Header(ulong VectorArrayAddress,
                                          int VectorCount,
                                          int Capacity,
                                          int ActiveRowCount,
                                          ulong AllocationBitmapAddress,
                                          ulong PureBitmapAddress,
                                          ulong SelectionAddress);
}
