using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

public class TableStructureProviderTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [Fact]
    public async Task Can_Get_TableStructure_With_Uniqueifier()
    {
        var metadata = await GetMetadata();

        var tableStructure = TableStructureProvider.GetTableStructure(metadata, 72057594054049792);

        Assert.NotEmpty(tableStructure.Columns);

        Assert.True(tableStructure.Columns[0].IsUniqueifier);
    }

    [Fact]
    public async Task Can_Get_Table_Structure()
    {
        var metadata = await GetMetadata();

        var tableStructure = TableStructureProvider.GetTableStructure(metadata, 458752);

        Assert.NotEmpty(tableStructure.Columns);

        Assert.Equal(11, tableStructure.Columns.Count);

        Assert.Equal("auid", tableStructure.Columns[0].ColumnName);
        Assert.Equal(System.Data.SqlDbType.BigInt, tableStructure.Columns[0].DataType);
        Assert.Equal(1, tableStructure.Columns[0].ColumnId);
        Assert.Equal(4, tableStructure.Columns[0].LeafOffset);
        Assert.Equal(1, tableStructure.Columns[0].NullBit);
        Assert.False(tableStructure.Columns[0].IsDropped);
        Assert.False(tableStructure.Columns[0].IsUniqueifier);

        Assert.False(tableStructure.Columns[0].IsSparse);

        Assert.Equal("type", tableStructure.Columns[1].ColumnName);
        Assert.Equal(System.Data.SqlDbType.TinyInt, tableStructure.Columns[1].DataType);
        Assert.Equal(2, tableStructure.Columns[1].ColumnId);
        Assert.Equal(12, tableStructure.Columns[1].LeafOffset);
        Assert.Equal(2, tableStructure.Columns[1].NullBit);
        Assert.False(tableStructure.Columns[1].IsDropped);
        Assert.False(tableStructure.Columns[1].IsUniqueifier);

        Assert.False(tableStructure.Columns[1].IsSparse);

        Assert.Equal("ownerid", tableStructure.Columns[2].ColumnName);
        Assert.Equal(System.Data.SqlDbType.BigInt, tableStructure.Columns[2].DataType);
        Assert.Equal(3, tableStructure.Columns[2].ColumnId);
        Assert.Equal(13, tableStructure.Columns[2].LeafOffset);
        Assert.Equal(3, tableStructure.Columns[2].NullBit);
        Assert.False(tableStructure.Columns[2].IsDropped);
        Assert.False(tableStructure.Columns[2].IsUniqueifier);

        Assert.False(tableStructure.Columns[2].IsSparse);

        Assert.Equal("status", tableStructure.Columns[3].ColumnName);

        Assert.Equal(System.Data.SqlDbType.Int, tableStructure.Columns[3].DataType);
        Assert.Equal(4, tableStructure.Columns[3].ColumnId);
        Assert.Equal(21, tableStructure.Columns[3].LeafOffset);
        Assert.Equal(4, tableStructure.Columns[3].NullBit);
        Assert.False(tableStructure.Columns[3].IsDropped);
        Assert.False(tableStructure.Columns[3].IsUniqueifier);

        Assert.False(tableStructure.Columns[3].IsSparse);

        Assert.Equal("fgid", tableStructure.Columns[4].ColumnName);
        Assert.Equal(System.Data.SqlDbType.SmallInt, tableStructure.Columns[4].DataType);
        Assert.Equal(5, tableStructure.Columns[4].ColumnId);
        Assert.Equal(25, tableStructure.Columns[4].LeafOffset);
        Assert.Equal(5, tableStructure.Columns[4].NullBit);
        Assert.False(tableStructure.Columns[4].IsDropped);
        Assert.False(tableStructure.Columns[4].IsUniqueifier);

        Assert.False(tableStructure.Columns[4].IsSparse);

        Assert.Equal("pgfirst", tableStructure.Columns[5].ColumnName);
        Assert.Equal(System.Data.SqlDbType.Binary, tableStructure.Columns[5].DataType);
        Assert.Equal(6, tableStructure.Columns[5].ColumnId);
        Assert.Equal(27, tableStructure.Columns[5].LeafOffset);
        Assert.Equal(6, tableStructure.Columns[5].NullBit);
        Assert.False(tableStructure.Columns[5].IsDropped);
        Assert.False(tableStructure.Columns[5].IsUniqueifier);

        Assert.False(tableStructure.Columns[5].IsSparse);

        Assert.Equal("pgroot", tableStructure.Columns[6].ColumnName);
        Assert.Equal(System.Data.SqlDbType.Binary, tableStructure.Columns[6].DataType);
        Assert.Equal(7, tableStructure.Columns[6].ColumnId);
        Assert.Equal(33, tableStructure.Columns[6].LeafOffset);
        Assert.Equal(7, tableStructure.Columns[6].NullBit);
        Assert.False(tableStructure.Columns[6].IsDropped);
        Assert.False(tableStructure.Columns[6].IsUniqueifier);

        Assert.False(tableStructure.Columns[6].IsSparse);

        Assert.Equal("pgfirstiam", tableStructure.Columns[7].ColumnName);

        Assert.Equal(System.Data.SqlDbType.Binary, tableStructure.Columns[7].DataType);
        Assert.Equal(8, tableStructure.Columns[7].ColumnId);
        Assert.Equal(39, tableStructure.Columns[7].LeafOffset);
        Assert.Equal(8, tableStructure.Columns[7].NullBit);
        Assert.False(tableStructure.Columns[7].IsDropped);
        Assert.False(tableStructure.Columns[7].IsUniqueifier);

        Assert.False(tableStructure.Columns[7].IsSparse);

        Assert.Equal("pcused", tableStructure.Columns[8].ColumnName);
        Assert.Equal(System.Data.SqlDbType.BigInt, tableStructure.Columns[8].DataType);
        Assert.Equal(9, tableStructure.Columns[8].ColumnId);
        Assert.Equal(45, tableStructure.Columns[8].LeafOffset);
        Assert.Equal(9, tableStructure.Columns[8].NullBit);
        Assert.False(tableStructure.Columns[8].IsDropped);

        Assert.False(tableStructure.Columns[8].IsUniqueifier);

        Assert.False(tableStructure.Columns[8].IsSparse);

        Assert.Equal("pcdata", tableStructure.Columns[9].ColumnName);
        Assert.Equal(System.Data.SqlDbType.BigInt, tableStructure.Columns[9].DataType);
        Assert.Equal(10, tableStructure.Columns[9].ColumnId);
        Assert.Equal(53, tableStructure.Columns[9].LeafOffset);
        Assert.Equal(10, tableStructure.Columns[9].NullBit);
        Assert.False(tableStructure.Columns[9].IsDropped);
        Assert.False(tableStructure.Columns[9].IsUniqueifier);

        Assert.False(tableStructure.Columns[9].IsSparse);

        Assert.Equal("pcreserved", tableStructure.Columns[10].ColumnName);
        Assert.Equal(System.Data.SqlDbType.BigInt, tableStructure.Columns[10].DataType);
        Assert.Equal(11, tableStructure.Columns[10].ColumnId);
        Assert.Equal(61, tableStructure.Columns[10].LeafOffset);
        Assert.Equal(11, tableStructure.Columns[10].NullBit);
        Assert.False(tableStructure.Columns[10].IsDropped);
        Assert.False(tableStructure.Columns[10].IsUniqueifier);

        Assert.False(tableStructure.Columns[10].IsSparse);

        Assert.Equal(458752, tableStructure.AllocationUnitId);
    }
}