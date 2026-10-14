using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class LocatorSearchTests
{
    private const ulong Page336 = 0x0000_0001_0000_0150;

    [Theory]
    [InlineData("1:336")]
    [InlineData("(1:336)")]
    [InlineData(" 1 : 336 ")]
    [InlineData("0x500100000100")]
    public void A_Page_Is_Read_From_Its_Address(string text)
    {
        Assert.True(LocatorSearch.TryParse(text, out var search));

        Assert.Equal(LocatorSearch.ForPage(new PageAddress(1, 336), null), search);
    }

    [Theory]
    [InlineData("1:336:5")]
    [InlineData("(1:336:5)")]
    [InlineData("0x5001000001000500")]
    public void A_Row_Is_Read_From_Its_Address_Or_Physloc(string text)
    {
        Assert.True(LocatorSearch.TryParse(text, out var search));

        Assert.Equal(LocatorSearch.ForPage(new PageAddress(1, 336), 5), search);
    }

    [Theory]
    [InlineData("")]
    [InlineData("336")]
    [InlineData("1:336:5:2")]
    [InlineData("a:b")]
    [InlineData("0x5001")]
    [InlineData("0x50010000010G")]
    public void Anything_Else_Is_Not_A_Page_Or_Row(string text) => Assert.False(LocatorSearch.TryParse(text, out _));

    [Fact]
    public void A_Page_Is_Read_From_The_Low_Six_Bytes_Whatever_Follows()
    {
        var locator = new Locator(1, LocatorValueType.Page, 0xABCD_0000_0000_0000 | Page336);

        Assert.Equal(new PageAddress(1, 336), locator.Page);
        Assert.Equal("(1:336)", locator.ToString());
    }

    [Fact]
    public void A_Row_Carries_Its_Slot_In_The_Top_Two_Bytes()
    {
        var locator = new Locator(4, LocatorValueType.Row, 0x0005_0000_0000_0000 | Page336);

        Assert.Equal("(1:336:5)", locator.ToString());
    }
}
