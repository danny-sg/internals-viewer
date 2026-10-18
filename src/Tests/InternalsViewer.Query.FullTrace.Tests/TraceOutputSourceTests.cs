using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Output.Sources;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceOutputSourceTests
{
    private const ulong Function = 0x7FF8_0030_0000;

    private const ulong Iterator = 0x1F4_0000_1000;

    private const ulong Buffer = 0x1F4_0000_2000;

    [Fact]
    public void A_Buffer_Column_Is_The_Column_At_Its_Address()
    {
        var row = new RowBuffer(Function, Iterator, 1, At(10), Iterator, Buffer, [Variant(0, 7), Variant(1, 42)], RowBufferFormat.VariantTable);

        Assert.Equal(1, new BufferOutputSource(0, Buffer + 0x20).GetColumn(row)?.Ordinal);
    }

    [Fact]
    public void A_Rowset_Column_Is_The_Bound_Column_Created_For_The_Same_Rowset_Ordinal()
    {
        var row = new RowBuffer(Function, Iterator, 1, At(10), Iterator, Buffer, [Bound(0, 1, 0x08), Bound(1, 3, 0x18)]);

        Assert.Equal(1, new RowsetOutputSource(0, 0) { Ordinal = 3 }.GetColumn(row)?.Ordinal);
    }

    [Fact]
    public void A_Rowset_Column_Not_In_The_Bound_Row_Is_In_The_Record_The_Scan_Is_On()
    {
        var location = new DeferredRecordValue(new PageAddress(1, 336), 2);

        var row = new RowBuffer(Function, Iterator, 1, At(10), Iterator, Buffer, [Bound(0, 1, 0x08)]) { Location = location };

        var column = new RowsetOutputSource(0, 0) { Ordinal = 3 }.GetColumn(row)!;

        Assert.Equal((3, (DeferredValue)location), (column.Ordinal, column.Deferred));
    }

    [Fact]
    public void A_Fixed_Hash_Column_Is_Cut_From_The_Record()
    {
        var column = new HashRecordOutputSource(0) { Ordinal = 2, Offset = 8, Length = 4 }.GetColumn(HashRow(nullBitmap: 0))!;

        Assert.Equal(0, column.Status);
        Assert.Equal([7, 0, 0, 0], column.Data);
    }

    [Fact]
    public void A_Null_Hash_Column_Keeps_The_Null_From_The_Record()
    {
        var column = new HashRecordOutputSource(0) { Ordinal = 2, Offset = 8, Length = 4 }.GetColumn(HashRow(nullBitmap: 0b10))!;

        Assert.Equal((3, 0), (column.Status, column.Data.Length));
    }

    [Fact]
    public void A_Variable_Hash_Column_Needs_The_Record_Layout()
        => Assert.Null(new HashRecordOutputSource(0) { Ordinal = 2, Offset = -1 }.GetColumn(HashRow(nullBitmap: 0)));

    [Fact]
    public void Every_Source_Type_Survives_Export()
    {
        TraceOutputSource[] sources =
        [
            new BufferOutputSource(0x10, Buffer) { Slot = 2, Owner = Iterator },
            new RowsetOutputSource(0x20, 0x30) { Slot = 1, Ordinal = 3, Owner = Iterator },
            new HashRecordOutputSource(0x40) { Slot = 4, Ordinal = 2, Offset = -1, Length = 0, Owner = Iterator },
            TraceOutputSource.Unknown
        ];

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            TraceOutputStructure.Write(writer, [new TraceOutputStructure(0x50, sources, 0)]);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        Assert.Equal(sources, Assert.Single(TraceOutputStructure.Read(reader)).Columns);
    }

    private static RowBuffer HashRow(byte nullBitmap)
    {
        byte[] record = [0x10, 0, 12, 0, 42, 0, 0, 0, 7, 0, 0, 0, 2, 0, nullBitmap];

        return new RowBuffer(Function, Iterator, 1, At(10), Iterator, Buffer, [], RowBufferFormat.HashTable) { Record = record };
    }

    private static RowBufferColumn Variant(int ordinal, byte value)
        => new(ordinal, RowBufferBinding.None, 0, RowBufferColumnFlags.None, 8, [value, 0, 0, 0, 0, 0, 0, 0]);

    private static RowBufferColumn Bound(int ordinal, int rowsetOrdinal, ulong valueOffset)
        => new(ordinal,
               new RowBufferBinding(-1, 0, 4, valueOffset) { Type = new RowBufferType(rowsetOrdinal, 56, 4, 10, 0, 0) },
               0,
               RowBufferColumnFlags.None,
               4,
               [1, 0, 0, 0]);
}
