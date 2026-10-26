using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using InternalsViewer.Query.FullTrace.Memory.History.Batches.Enums;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History.Batches;

[Trait("Category", "Unit")]
public class BatchReaderTests
{
    private const ulong Header = 0x1000;

    private const ulong VectorArray = 0x2000;

    private const ulong AllocationBitmap = 0x2800;

    private const ulong PureBitmap = 0x3000;

    private const ulong Selection = 0x4000;

    private const ulong FirstBuffer = 0x5040;

    private const ulong SecondBuffer = 0x6000;

    [Fact]
    public void Reads_The_Header_And_Every_Vector_By_Id()
    {
        var batch = BatchReader.Read(GetMemory(), Header)!;

        Assert.Equal((900, 2, 2), (batch.Capacity, batch.ActiveRowCount, batch.Vectors.Count));
        Assert.Equal([(1, FirstBuffer), (2, SecondBuffer)], batch.Vectors.Select(v => (v.Id, v.Buffer)));
    }

    [Fact]
    public void Rows_Run_To_The_Highest_Selected_Row()
    {
        var batch = BatchReader.Read(GetMemory(), Header)!;

        Assert.Equal([(ushort)0, (ushort)2], batch.Selection);
        Assert.Equal(3, batch.RowCount);
    }

    [Fact]
    public void A_Pure_Vector_Holds_Only_Its_First_Slot()
    {
        var pure = BatchReader.Read(GetMemory(), Header)!.Vectors[1];

        Assert.True(pure.IsPure);
        Assert.Equal([0x77UL], pure.Slots);
    }

    [Fact]
    public void Slots_That_Were_Never_Written_Are_Unknown()
    {
        var vector = BatchReader.Read(GetMemory(), Header)!.Vectors[0];

        Assert.Equal([true, true, false], vector.Known);
        Assert.Equal([0x10UL, 0x20UL], vector.Slots.Take(2));
    }

    [Fact]
    public void Each_Part_Is_Reported_With_Its_Role()
        => Assert.Equal([(VectorArray, new BatchPart(BatchPartType.VectorArray)),
                         (AllocationBitmap, new BatchPart(BatchPartType.AllocationBitmap)),
                         (PureBitmap, new BatchPart(BatchPartType.PureBitmap)),
                         (Selection, new BatchPart(BatchPartType.Selection)),
                         (FirstBuffer, new BatchPart(BatchPartType.Vector, 1)),
                         (0, new BatchPart(BatchPartType.DeepData, 1)),
                         (SecondBuffer, new BatchPart(BatchPartType.Vector, 2)),
                         (0, new BatchPart(BatchPartType.DeepData, 2))],
                        BatchReader.GetParts(GetMemory(), Header));

    [Fact]
    public void A_Header_That_Cannot_Be_Read_Is_No_Batch()
        => Assert.Null(BatchReader.Read(new Memory(), Header));

    private static Memory GetMemory()
    {
        var header = new byte[0x40];

        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x00), AllocationBitmap);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x08), VectorArray);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x10), PureBitmap);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0x1C), 2);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0x24), 900);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0x28), 2);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x38), Selection);

        var vectors = new byte[2 * 0x58];

        BinaryPrimitives.WriteUInt64LittleEndian(vectors.AsSpan(0), FirstBuffer);
        BinaryPrimitives.WriteUInt64LittleEndian(vectors.AsSpan(0x58), SecondBuffer);

        return new Memory().Add(Header, header)
                           .Add(VectorArray, vectors)
                           .Add(PureBitmap, [0b10])
                           .Add(Selection, [0, 0, 2, 0])
                           .Add(FirstBuffer, [0x10, 0, 0, 0, 0, 0, 0, 0, 0x20, 0, 0, 0, 0, 0, 0, 0])
                           .Add(SecondBuffer, [0x77, 0, 0, 0, 0, 0, 0, 0]);
    }

    private sealed class Memory : IMemoryReader
    {
        private List<(ulong Address, byte[] Data)> Regions { get; } = [];

        public Memory Add(ulong address, byte[] data)
        {
            Regions.Add((address, data));

            return this;
        }

        public int Read(ulong address, Span<byte> destination)
        {
            foreach (var (start, data) in Regions)
            {
                if (address >= start && address < start + (ulong)data.Length)
                {
                    var offset = (int)(address - start);

                    var count = Math.Min(destination.Length, data.Length - offset);

                    data.AsSpan(offset, count).CopyTo(destination);

                    return count;
                }
            }

            return 0;
        }
    }
}
