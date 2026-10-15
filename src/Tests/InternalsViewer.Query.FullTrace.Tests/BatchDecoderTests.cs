using System.Data;
using System.Text;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.FullTrace.Batches;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class BatchDecoderTests
{
    [Fact]
    public void An_Integer_Slot_Holds_The_Value_Shifted_Past_The_Tag()
        => Assert.Equal("3146000", BatchDecoder.DecodeSlot(0x600220, null, Column(SqlDbType.Int)));

    [Fact]
    public void A_Date_Slot_Holds_Its_Day_Number()
        => Assert.Equal("2022-02-07", BatchDecoder.DecodeSlot(0x168720, null, Column(SqlDbType.Date)));

    [Fact]
    public void A_Float_Slot_Keeps_Its_Top_Bits_In_Place()
        => Assert.Equal("1E-10", BatchDecoder.DecodeSlot(0x3BB6F9BFB3AF7B76, null, Column(SqlDbType.Float)));

    [Fact]
    public void A_String_Slot_Without_The_Tag_Is_A_Dictionary_Reference()
        => Assert.Equal("Dictionary 128 Entry 230", BatchDecoder.DecodeSlot(0xE60100, null, Column(SqlDbType.VarChar)));

    [Fact]
    public void A_Computed_String_References_The_Query_Dictionary()
        => Assert.Equal("Dictionary 0 Entry 26338", BatchDecoder.DecodeSlot(0x66E20000, null, Column(SqlDbType.VarChar)));

    [Fact]
    public void A_Deep_String_Is_Read_From_Its_Captured_Bytes()
    {
        var deep = new BatchDeepValue(14, Encoding.ASCII.GetBytes("REF-0003146000"));

        Assert.Equal("REF-0003146000", BatchDecoder.DecodeSlot(0x20EF6E5005D, deep, Column(SqlDbType.VarChar)));
    }

    [Fact]
    public void A_Deep_Value_Captured_In_Part_Is_Not_Decoded()
    {
        var deep = new BatchDeepValue(14, Encoding.ASCII.GetBytes("REF-000"));

        Assert.Null(BatchDecoder.DecodeSlot(0x20EF6E5005D, deep, Column(SqlDbType.VarChar)));
    }

    [Fact]
    public void A_Slot_Of_One_Is_Null()
        => Assert.Equal("NULL", BatchDecoder.DecodeSlot(1, null, Column(SqlDbType.Int)));

    private static ColumnStructure Column(SqlDbType dataType) => new() { ColumnName = "Column", DataType = dataType };
}
