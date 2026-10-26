using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Batches;
using static InternalsViewer.Query.FullTrace.Tests.Memory.History.MemoryHistoryStores;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History.Batches;

[Trait("Category", "Unit")]
public class MemoryHistoryBatchesTests
{
    private const ulong Header = 0x21C_0000_1000;

    private const ulong VectorArray = 0x21C_0000_2000;

    private const ulong Selection = 0x21C_0000_3000;

    private const ulong FirstBuffer = 0x21C_0000_4000;

    private const ulong SecondBuffer = 0x21C_0000_5000;

    private const ulong Other = 0x21C_0000_6000;

    private const ulong BufferOffset = 0x10;

    private static readonly MemoryHistoryStore Store = BuildStore();

    private static readonly MemoryHistoryBatches Batches = new(Store, GetObjectName, GetAllocatingFunction);

    [Fact]
    public void A_Vector_Buffer_Finds_Its_Batch_And_Its_Vector()
    {
        var batch = Batches.Read(GetBlock(Store, SecondBuffer), At(3))!;

        Assert.Equal((GetBlock(Store, Header), 2), (batch.Block, batch.VectorId));
    }

    [Fact]
    public void A_Header_Reads_Its_Own_Batch()
    {
        var batch = Batches.Read(GetBlock(Store, Header), At(3))!;

        Assert.Equal((0, 2, 2), (batch.VectorId, batch.State.Vectors.Count, batch.State.RowCount));
        Assert.Equal([0xAAUL, 0xBBUL], batch.State.Vectors[0].Slots);
    }

    [Fact]
    public void There_Is_No_Batch_Before_Its_Header_Is_Written()
        => Assert.Null(Batches.Read(GetBlock(Store, Header), At(1)));

    [Fact]
    public void Batch_Parts_Are_Named_By_Their_Role_In_The_Batch()
        => Assert.Equal(["CBpBatch", "CBpBatch Vector Array", "CBpBatch Selection Vector", "CBpBatch Vector 2", null],
                        new[] { Header, VectorArray, Selection, SecondBuffer, Other }
                            .Select(a => Batches.GetPart(GetBlock(Store, a))?.Name));

    [Fact]
    public void The_Batch_Changes_When_A_Vector_Buffer_Is_Written()
    {
        var batch = Batches.Read(GetBlock(Store, Header), At(3))!;

        Assert.Equal((false, true), (batch.HasChanged(At(3, 1)), batch.HasChanged(At(5))));
    }

    private static string? GetObjectName(int block) => Store.Blocks[block].Address == Header ? BatchReader.ClassName : null;

    private static string? GetAllocatingFunction(int block)
        => Store.Blocks[block].Address == Other ? "CMemObj::Alloc" : "CBpBatch::AllocateVectorBuffer";

    private static MemoryHistoryStore BuildStore()
    {
        var header = new byte[0x40];

        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x08), VectorArray);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0x1C), 2);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0x24), 900);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(0x28), 2);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x38), Selection);

        var vectors = new byte[2 * 0x58];

        BinaryPrimitives.WriteUInt64LittleEndian(vectors.AsSpan(0), FirstBuffer + BufferOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(vectors.AsSpan(0x58), SecondBuffer + BufferOffset);

        return Build([Block(Header, 0x40), Block(VectorArray, 0xB0), Block(Selection, 0x10),
                      Block(FirstBuffer, 0x40), Block(SecondBuffer, 0x40), Block(Other, 0x10)],
                     [(2, Header, header),
                      (2, VectorArray, vectors),
                      (2, Selection, [0, 0, 1, 0]),
                      (2, FirstBuffer + BufferOffset, [0xAA, 0, 0, 0, 0, 0, 0, 0, 0xBB, 0, 0, 0, 0, 0, 0, 0]),
                      (2, SecondBuffer + BufferOffset, [0xCC, 0, 0, 0, 0, 0, 0, 0, 0xDD, 0, 0, 0, 0, 0, 0, 0]),
                      (2, Other, [1]),
                      (4, FirstBuffer + BufferOffset, [0xEE])]);
    }
}
