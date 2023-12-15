using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Parsers;

namespace InternalsViewer.Tests.Internals.UnitTests.Engine.Parsers;

public class PageAddressParserTests
{
    [Theory]
    [InlineData("1:9", 1, 9)]
    [InlineData("(1:9)", 1, 9)]
    [InlineData("(01:09)", 1, 9)]
    [InlineData("2:1000", 2, 1000)]
    [InlineData("0x000000000000", 0, 0)]
    [InlineData("0x521A00000100", 1, 6738)]
    [InlineData("0x111111111111", 4369, 286331153)]
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
}