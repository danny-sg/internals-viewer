using System.Buffers.Binary;
using System.Data;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.FullTrace.Memory.History.Pages;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History.Pages;

[Trait("Category", "Unit")]
public class WorkfilePageReaderTests
{
    private const int PageSize = 8192;

    private const byte WorkFile = 6;

    private const byte Data = 1;

    private const byte Sort = 7;

    private const int RecordOffset = 0x60;

    [Fact]
    public void A_Workfile_Record_Is_Split_Into_Columns_When_The_Layout_Fits()
    {
        var page = WorkfilePageReader.Read(Page(WorkFile, 5001), Layout());

        var record = Assert.Single(page!.Records);

        Assert.Equal("5001", Assert.Single(record.Fields).Value);
        Assert.Equal(0, record.Slot);
        Assert.True(page.HasValues);
    }

    [Fact]
    public void A_Workfile_Record_Without_A_Layout_Shows_Its_Structure_Only()
    {
        var page = WorkfilePageReader.Read(Page(WorkFile, 5001), null);

        var record = Assert.Single(page!.Records);

        Assert.Equal((1, 8), ((int)record.ColumnCount, (int)record.ColumnCountOffset));
        Assert.Empty(record.Fields);
        Assert.False(page.HasValues);
    }

    [Fact]
    public void A_Layout_That_Does_Not_Fit_The_Record_Is_Not_Used()
    {
        var layout = new RowRecordLayout([Column("Id", SqlDbType.BigInt, 8)]);

        var record = Assert.Single(WorkfilePageReader.Read(Page(WorkFile, 5001), layout)!.Records);

        Assert.Empty(record.Fields);
    }

    [Fact]
    public void A_Sort_Record_Shows_Its_Index_Record_Structure()
    {
        var data = new byte[PageSize];

        data[0] = 1;
        data[1] = Sort;

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x0E), 15);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x16), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(PageSize - 2), RecordOffset);

        Convert.FromHexString("36183C0100526F77203830393230200300000100230052" + "6F772049643A203830393230")
               .CopyTo(data.AsSpan(RecordOffset));

        var record = Assert.Single(WorkfilePageReader.Read(data, Layout())!.Records);

        Assert.Equal((3, 1, 1), ((int)record.ColumnCount, (int)record.NullBitmapSize, (int)record.VariableLengthColumnCount));
        Assert.Equal([0x23], record.VariableLengthColumnOffsetArray);
    }

    [Fact]
    public void A_Sort_Record_Is_Read_With_The_Sort_Input_Columns()
    {
        var record = Assert.Single(WorkfilePageReader.Read(SortPage(), null, SortLayout(SqlDbType.Int, 4))!.Records);

        Assert.Equal(["Id", "TextField (Sort Key)", "FixedTextField"], record.Fields.Select(f => f.Name));
        Assert.Equal(["100", "Row Id: 100", "Row 100"], record.Fields.Select(f => f.Value.TrimEnd()));
    }

    [Fact]
    public void A_Sort_Layout_With_A_Different_Fixed_Length_Is_Not_Used()
    {
        var record = Assert.Single(WorkfilePageReader.Read(SortPage(), null, SortLayout(SqlDbType.BigInt, 8))!.Records);

        Assert.Empty(record.Fields);
    }

    [Fact]
    public void A_Page_That_Is_Not_A_Workspace_Page_Is_Not_Read()
        => Assert.Null(WorkfilePageReader.Read(Page(Data, 5001), Layout()));

    [Fact]
    public void A_Hash_Table_Row_Holds_The_Build_Columns_Then_The_Build_Keys()
    {
        var hashMatch = new PlanNode
        {
            HashInfo = new HashInfo { BuildKeys = [Reference("Id")] },
            Children = [new PlanNode { OutputColumns = [Reference("Id"), Reference("TextField"), Reference("RowSpacer")] }]
        };

        Assert.Equal(["Id", "TextField", "RowSpacer", "Id"], HashTableColumns.Get(hashMatch).Select(c => c.Column));
        Assert.Equal(1, HashTableColumns.GetKeyCount(hashMatch));
    }

    [Fact]
    public void Build_Key_Columns_Are_Named_As_Keys()
    {
        var layout = new RowRecordLayout([Column("Id", SqlDbType.Int, 4), Column("Id", SqlDbType.Int, 4)], keyCount: 1);

        Assert.Equal(["Id", "Id (Build Key)"], layout.Structure.Columns.Select(c => c.ColumnName));
    }

    private static RowRecordLayout Layout() => new([Column("Id", SqlDbType.Int, 4)]);

    private static SortRecordLayout SortLayout(SqlDbType idType, short idLength)
    {
        List<ColumnStructure> columns =
        [
            Column("Id", idType, idLength),
            Column("TextField", SqlDbType.VarChar, 100),
            Column("FixedTextField", SqlDbType.Char, 10)
        ];

        return SortRecordLayout.Create(columns.Count, o => columns[o], o => o == 1)!;
    }

    private static byte[] SortPage()
    {
        var data = new byte[PageSize];

        data[0] = 1;
        data[1] = Sort;

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x0E), 15);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x16), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(PageSize - 2), RecordOffset);

        Convert.FromHexString("2664000000526F7720313030202020" + "01001E00" + "526F772049643A20313030").CopyTo(data.AsSpan(RecordOffset));

        return data;
    }

    private static ColumnStructure Column(string name, SqlDbType type, short length)
        => new() { ColumnName = name, DataType = type, DataLength = length };

    private static ColumnReference Reference(string name) => new() { Table = "[ClusteredTable]", Column = name };

    private static byte[] Page(byte type, int id)
    {
        var data = new byte[PageSize];

        data[0] = 1;
        data[1] = type;

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x16), 1);

        data[RecordOffset] = 0x10;

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(RecordOffset + 2), 8);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(RecordOffset + 4), id);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(RecordOffset + 8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(PageSize - 2), RecordOffset);

        return data;
    }
}
