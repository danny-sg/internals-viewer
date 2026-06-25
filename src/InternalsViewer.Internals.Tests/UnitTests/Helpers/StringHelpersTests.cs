using System.Collections;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Helpers;

public class StringHelpersTests
{
    [Theory]
    [InlineData(new byte[] { 0x00 }, "00")]
    [InlineData(new byte[] { 0xFF }, "FF")]
    [InlineData(new byte[] { 0xDE, 0xAD }, "DEAD")]
    [InlineData(new byte[] { 0x01, 0x23, 0x45, 0x67 }, "01234567")]
    public void ToHexString_Array_Returns_Uppercase(byte[] input, string expected)
    {
        Assert.Equal(expected, StringHelpers.ToHexString(input));
    }

    [Fact]
    public void ToHexString_Null_Returns_Empty()
    {
        Assert.Equal(string.Empty, StringHelpers.ToHexString((byte[]?)null));
    }

    [Theory]
    [InlineData((byte)0x00, "00")]
    [InlineData((byte)0x0F, "0F")]
    [InlineData((byte)0xFF, "FF")]
    public void ToHexString_Single_Byte_Returns_Two_Chars(byte input, string expected)
    {
        Assert.Equal(expected, StringHelpers.ToHexString(input));
    }

    [Theory]
    [InlineData("HelloWorld", "Hello World")]
    [InlineData("PageType", "Page Type")]
    [InlineData("GAMPage", "G A M Page")] // consecutive caps each get a space (regex \B[A-Z])
    [InlineData("allocationUnit", "allocation Unit")]
    [InlineData("simple", "simple")]
    public void SplitCamelCase_Inserts_Space_Before_Capitals(string input, string expected)
    {
        Assert.Equal(expected, input.SplitCamelCase());
    }

    [Fact]
    public void GetBitArrayString_Returns_Reversed_Binary()
    {
        // BitArray with bit 0 set
        var bits = new BitArray(8, false);
        bits[0] = true;

        var result = StringHelpers.GetBitArrayString(bits);

        Assert.Equal(8, result.Length);
        Assert.Equal('1', result[^1]); // bit 0 is at the end after reversal
        Assert.All(result[..^1].ToCharArray(), c => Assert.Equal('0', c));
    }

    [Fact]
    public void ToByteArray_Parses_Hex_String()
    {
        var result = "DEADBEEF".ToByteArray();

        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, result);
    }

    [Fact]
    public void ToByteArray_Ignores_Spaces()
    {
        var result = "DE AD BE EF".ToByteArray();

        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, result);
    }

    [Fact]
    public void CleanHex_Removes_Non_Hex_Characters()
    {
        // '0' in "0x" is a valid hex digit so it is kept; only 'x' is removed
        var result = "0xDE-AD:BE EF!".CleanHex();

        Assert.Equal("0DEADBEEF", result);
    }

    [Fact]
    public void CleanHex_Strips_All_Separators_When_No_0x_Prefix()
    {
        var result = "DE-AD:BE EF!".CleanHex();

        Assert.Equal("DEADBEEF", result);
    }
}
