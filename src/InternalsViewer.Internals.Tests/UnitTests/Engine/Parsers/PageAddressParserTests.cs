using InternalsViewer.Internals.Engine.Parsers;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Parsers;

public class PageAddressParserTests
{
    [Theory]
    [InlineData("1:9", 1, 9)]
    [InlineData("(1:9)", 1, 9)]
    [InlineData("(01:09)", 1, 9)]
    [InlineData("2:1000", 2, 1000)]
    [InlineData("0x000000000000", 0, 0)]
    [InlineData("0x521A00000100", 1, 6738)]
    [InlineData("0x140000000100", 1, 20)]
    [InlineData("0x111111111111", 4369, 286331153)]
    [InlineData("0xDF0200000100", 1, 735)]
    public void Can_Parse_Page_Address(string value, int expectedFileId, int expectedPageId)
    {
        var pageAddress = PageAddressParser.Parse(value);

        Assert.Equal(expectedFileId, pageAddress.FileId);
        Assert.Equal(expectedPageId, pageAddress.PageId);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1-10")]
    [InlineData("1:ABC")]
    [InlineData("0x00")]
    public void Invalid_Format_Throws_Exception(string value)
    {
        Assert.Throws<ArgumentException>(() => PageAddressParser.Parse(value));
    }

    [Theory]
    [InlineData("1:9", 1, 9)]
    [InlineData("0x140000000100", 1, 20)]
    public void TryParse_Returns_True_For_Valid_Address(string value, int expectedFileId, int expectedPageId)
    {
        var result = PageAddressParser.TryParse(value, out var pageAddress);

        Assert.True(result);
        Assert.Equal(expectedFileId, pageAddress.FileId);
        Assert.Equal(expectedPageId, pageAddress.PageId);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("not-an-address")]
    [InlineData("0x00")]
    public void TryParse_Returns_False_For_Invalid_Address(string value)
    {
        var result = PageAddressParser.TryParse(value, out var pageAddress);

        Assert.False(result);
        Assert.Equal(PageAddress.Empty, pageAddress);
    }

    [Fact]
    public void Parse_Bytes_Reads_PageId_Then_FileId()
    {
        // 4 bytes page id (little endian) followed by 2 bytes file id.
        var data = new byte[] { 0x14, 0x00, 0x00, 0x00, 0x01, 0x00 };

        var pageAddress = PageAddressParser.Parse(data);

        Assert.Equal(1, pageAddress.FileId);
        Assert.Equal(20, pageAddress.PageId);
    }

    [Fact]
    public void Parse_Bytes_At_Offset_Reads_From_Start_Address()
    {
        var data = new byte[] { 0xFF, 0xFF, 0x14, 0x00, 0x00, 0x00, 0x01, 0x00 };

        var pageAddress = PageAddressParser.Parse(data, 2);

        Assert.Equal(1, pageAddress.FileId);
        Assert.Equal(20, pageAddress.PageId);
    }

    [Fact]
    public void Parse_Span_At_Offset_Reads_From_Start_Address()
    {
        ReadOnlySpan<byte> data = new byte[] { 0xFF, 0x14, 0x00, 0x00, 0x00, 0x01, 0x00 };

        var pageAddress = PageAddressParser.Parse(data, 1);

        Assert.Equal(1, pageAddress.FileId);
        Assert.Equal(20, pageAddress.PageId);
    }

    [Fact]
    public void ParseBytes_Parses_Hex_String_With_Prefix()
    {
        var pageAddress = PageAddressParser.ParseBytes("0x140000000100");

        Assert.Equal(1, pageAddress.FileId);
        Assert.Equal(20, pageAddress.PageId);
    }

    [Fact]
    public void ToPageAddress_Null_Array_Returns_Empty()
    {
        byte[]? data = null;

        var pageAddress = data.ToPageAddress();

        Assert.Equal(PageAddress.Empty, pageAddress);
    }

    [Fact]
    public void ToPageAddress_Array_Parses_Six_Bytes()
    {
        var data = new byte[] { 0x14, 0x00, 0x00, 0x00, 0x01, 0x00 };

        var pageAddress = data.ToPageAddress();

        Assert.Equal(new PageAddress(1, 20), pageAddress);
    }

    [Fact]
    public void ToPageAddress_Array_Invalid_Length_Throws()
    {
        var data = new byte[] { 0x14, 0x00, 0x00 };

        Assert.Throws<ArgumentException>(() => data.ToPageAddress());
    }

    [Fact]
    public void ToPageAddress_Span_Invalid_Length_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<byte> data = new byte[] { 0x14, 0x00, 0x00 };

            data.ToPageAddress();
        });
    }
}