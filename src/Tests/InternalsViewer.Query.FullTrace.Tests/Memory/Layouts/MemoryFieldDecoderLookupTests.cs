using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts;

[Trait("Category", "Unit")]
public class MemoryFieldDecoderLookupTests
{
    [Theory]
    [InlineData(0x0UL, "Released")]
    [InlineData(0x1000UL, "Shared, 1 Holder")]
    [InlineData(0x3000UL, "Shared, 3 Holders")]
    [InlineData(0x2UL, "Exclusive")]
    [InlineData(0xAUL, "Exclusive, Waiters")]
    public void A_Latch_State_Is_Read_As_Its_Mode(ulong state, string expected)
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(MemoryFieldDecoder.Latch, state));

    [Theory]
    [InlineData(56UL, "Int")]
    [InlineData(167UL, "VarChar")]
    [InlineData(0xFFUL, "0xFF")]
    public void A_Type_Is_Read_As_Its_Sql_Type(ulong type, string expected)
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(MemoryFieldDecoder.SqlType, type));

    [Theory]
    [InlineData(0x0UL, "None")]
    [InlineData(0x900UL, "Length, By Reference")]
    [InlineData(0x4000UL, "Large Object")]
    public void Binding_Flags_Are_Read_As_Their_Names(ulong flags, string expected)
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(MemoryFieldDecoder.BindingFlags, flags));

    [Theory]
    [InlineData(0xAUL, "10")]
    [InlineData(0xFFFFFFFEUL, "Variable 2")]
    public void A_Negative_Record_Offset_Is_A_Variable_Column(ulong offset, string expected)
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(MemoryFieldDecoder.RecordOffset, offset));

    [Fact]
    public void A_Field_Whose_Kind_Is_A_Decoder_Uses_It()
        => Assert.Equal(MemoryFieldDecoder.Latch, EngineLayout.Latest.GetStructure("BUF").Get("Latch State").Decoder);
}
