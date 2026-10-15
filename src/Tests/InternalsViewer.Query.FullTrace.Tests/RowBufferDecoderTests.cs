using System.Data;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class RowBufferDecoderTests
{
    [Fact]
    public void Columns_Are_Found_By_Their_Leaf_Offset()
    {
        var columns = RowBufferDecoder.GetColumns(Table(Column("Id", 4, SqlDbType.Int),
                                                        Column("Padding", 8, SqlDbType.Char),
                                                        Column("Name", -1, SqlDbType.VarChar)));

        Assert.Equal(("Id", "Padding", "Name"), (columns[4].ColumnName, columns[8].ColumnName, columns[-1].ColumnName));
    }

    [Fact]
    public void Columns_Sharing_A_Leaf_Offset_Are_Left_Undecoded()
    {
        var columns = RowBufferDecoder.GetColumns(Table(Column("Id", 4, SqlDbType.Int),
                                                        Column("IsActive", 8, SqlDbType.Bit),
                                                        Column("IsDeleted", 8, SqlDbType.Bit)));

        Assert.Equal([4], columns.Keys);
    }

    [Fact]
    public void Dropped_Columns_Are_Left_Out()
    {
        var columns = RowBufferDecoder.GetColumns(Table(Column("Id", 4, SqlDbType.Int),
                                                        Column("Old", 8, SqlDbType.Int) with { IsDropped = true },
                                                        Column("New", 8, SqlDbType.Int)));

        Assert.Equal("New", columns[8].ColumnName);
    }

    [Fact]
    public void An_Inline_Value_Is_Read_From_Its_Storage_Bytes()
    {
        var column = Value(RowBufferColumnFlags.None, [0x20, 0x28, 0x30, 0, 0, 0, 0, 0]);

        var structure = new ColumnStructure { ColumnName = "Quantity", DataType = SqlDbType.Int, DataLength = 4 };

        Assert.Equal("3156000", RowBufferDecoder.DecodeValue(column, structure));
    }

    [Fact]
    public void A_String_Value_Is_Read_Through_Its_Pointer()
    {
        var column = Value(RowBufferColumnFlags.ByReference | RowBufferColumnFlags.HasLength, "SYM000"u8.ToArray());

        var structure = new ColumnStructure { ColumnName = "Symbol", DataType = SqlDbType.VarChar, DataLength = 10 };

        Assert.Equal("SYM000", RowBufferDecoder.DecodeValue(column, structure));
    }

    [Fact]
    public void A_String_Without_A_Length_Is_Empty()
    {
        var column = Value(RowBufferColumnFlags.None, [0x68, 0xC0, 0xB7, 0x1E, 0x1C, 0x02, 0, 0]);

        var structure = new ColumnStructure { ColumnName = "Symbol", DataType = SqlDbType.VarChar, DataLength = 10 };

        Assert.Equal(string.Empty, RowBufferDecoder.DecodeValue(column, structure));
    }

    private static RowBufferColumn Value(RowBufferColumnFlags flags, byte[] data)
        => new(0, RowBufferBinding.None, 0, flags, data.Length, data);

    private static TableStructure Table(params ColumnStructure[] columns) => new(1) { Columns = [.. columns] };

    private static ColumnStructure Column(string name, short leafOffset, SqlDbType type)
        => new() { ColumnName = name, LeafOffset = leafOffset, DataType = type };
}
