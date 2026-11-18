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
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(EngineLayout.Latest, MemoryFieldDecoder.Latch, state));

    [Theory]
    [InlineData(56UL, "Int")]
    [InlineData(167UL, "VarChar")]
    [InlineData(0xFFUL, "0xFF")]
    public void A_Type_Is_Read_As_Its_Sql_Type(ulong type, string expected)
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(EngineLayout.Latest, MemoryFieldDecoder.SqlType, type));

    [Theory]
    [InlineData(0x0UL, "None")]
    [InlineData(0x900UL, "Length, By Reference")]
    [InlineData(0x4000UL, "Large Object")]
    public void Binding_Flags_Are_Read_As_Their_Names(ulong flags, string expected)
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(EngineLayout.Latest, MemoryFieldDecoder.BindingFlags, flags));

    [Theory]
    [InlineData(0xAUL, "10")]
    [InlineData(0xFFFFFFFEUL, "Variable 2")]
    public void A_Negative_Record_Offset_Is_A_Variable_Column(ulong offset, string expected)
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(EngineLayout.Latest, MemoryFieldDecoder.RecordOffset, offset));

    [Theory]
    [InlineData(0x0UL, "00000000")]
    [InlineData(0x6UL, "01100000")]
    [InlineData(0x101UL, "10000000 10000000")]
    public void A_Bitmap_Is_Read_Lowest_Bit_First_A_Byte_At_A_Time(ulong bits, string expected)
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(EngineLayout.Latest, MemoryFieldDecoder.Bitmap, bits));

    [Theory]
    [InlineData(0x3800000138UL, "int NOT NULL")]
    [InlineData(0xA7000003A7UL, "varchar NOT NULL")]
    public void A_Type_Info_Header_Names_The_Type_And_Its_Nullability(ulong header, string expected)
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(EngineLayout.Latest, MemoryFieldDecoder.TypeInfo, header));

    [Theory]
    [InlineData(0xA0004UL, "Maximum Length 4, Precision 10, Scale 0")]
    [InlineData(0x3400D00800000064UL, "Maximum Length 100, Precision 0, Scale 0, Collation 872468488")]
    public void Type_Details_Give_The_Length_Precision_Scale_And_Collation(ulong details, string expected)
        => Assert.Equal(expected, MemoryFieldDecoderLookup.GetText(EngineLayout.Latest, MemoryFieldDecoder.TypeDetails, details));

    [Fact]
    public void A_Value_Reference_Reads_Its_Type_With_The_Type_Decoders()
        => Assert.Equal((MemoryFieldDecoder.TypeInfo, MemoryFieldDecoder.TypeDetails),
                        (EngineLayout.Latest.GetStructure("CValRefComp").Get("Type").Decoder,
                         EngineLayout.Latest.GetStructure("CValRefComp").Get("Type Details").Decoder));

    [Fact]
    public void A_Field_Whose_Kind_Is_A_Decoder_Uses_It()
        => Assert.Equal(MemoryFieldDecoder.Latch, EngineLayout.Latest.GetStructure("BUF").Get("Latch State").Decoder);
}
