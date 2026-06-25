using InternalsViewer.Internals.Extensions;

namespace InternalsViewer.Internals.Tests.UnitTests.Extensions;

public class ByteExtensionsTests
{
    [Theory]
    [InlineData(0b00000000, "00000000")]
    [InlineData(0b00000001, "10000000")]  // BitArray ordering is LSB-first
    [InlineData(0b10000000, "00000001")]
    [InlineData(0b11111111, "11111111")]
    [InlineData(0b10101010, "01010101")]
    public void ToBinaryString_Returns_Bit_String(byte input, string expected)
    {
        Assert.Equal(expected, input.ToBinaryString());
    }

    [Theory]
    [InlineData(0x00, "00")]
    [InlineData(0xFF, "FF")]
    [InlineData(0x0A, "0A")]
    [InlineData(0xAB, "AB")]
    public void ToHexString_Byte_Returns_Two_Char_Uppercase(byte input, string expected)
    {
        Assert.Equal(expected, input.ToHexString());
    }

    [Theory]
    [InlineData(new byte[] { 0x00 }, "00")]
    [InlineData(new byte[] { 0xFF, 0x0A }, "FF0A")]
    [InlineData(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, "DEADBEEF")]
    [InlineData(new byte[] { }, "")]
    public void ToHexString_ByteArray_Returns_Uppercase_No_Spaces(byte[] input, string expected)
    {
        Assert.Equal(expected, input.ToHexString());
    }
}
