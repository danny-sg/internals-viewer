using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class SqlTypeInfoTests
{
    [Theory]
    [InlineData(0x0000003800000138ul, 0x00000000000A0004ul, "int NOT NULL")]
    [InlineData(0x000000E7000000E7ul, 0xFFFFFFFF00000100ul, "nvarchar(128)")]
    [InlineData(0x000000A5000000A5ul, 0x000000000000FFFFul, "varbinary(max)")]
    [InlineData(0x000000A7000000A7ul, 0xFFFFFFFF00001F40ul, "varchar(8000)")]
    [InlineData(0x0000006A0000006Aul, 0x0000000000260011ul, "decimal(38,0)")]
    [InlineData(0x0000002A0000012Aul, 0x00000000071B0008ul, "datetime2(7) NOT NULL")]
    [InlineData(0x0000003C0000003Cul, 0x0000000004130008ul, "money")]
    [InlineData(0x0000000100000001ul, 0ul, "type 0x01")]
    public void A_Type_Is_Written_As_In_TSql(ulong header, ulong details, string expected)
        => Assert.Equal(expected, SqlTypeInfo.From(header, details).ToString());

    [Fact]
    public void A_Type_Without_Its_Length_Is_Written_Without_One()
        => Assert.Equal("nvarchar NOT NULL", SqlTypeInfo.From(0x01E7, 0).ToString());
}
