using System.Data;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.FullTrace.RowBuffers;

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

    private static TableStructure Table(params ColumnStructure[] columns) => new(1) { Columns = [.. columns] };

    private static ColumnStructure Column(string name, short leafOffset, SqlDbType type)
        => new() { ColumnName = name, LeafOffset = leafOffset, DataType = type };
}
