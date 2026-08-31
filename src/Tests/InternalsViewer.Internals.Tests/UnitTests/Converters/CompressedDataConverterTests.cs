using System.Data;
using InternalsViewer.Internals.Converters;

namespace InternalsViewer.Internals.Tests.UnitTests.Converters;

[Trait("Category", "Unit")]
[Trait("Area", "Converters")]
public class CompressedDataConverterTests
{
    [Theory]
    [InlineData(new byte[] { 0xF0, 0x3F }, 1)]
    [InlineData(new byte[] { 0xF0, 0xBF }, -1)]
    public void Float_Expands_Truncated_Trailing_Bytes(byte[] data, double expected)
    {
        var result = CompressedDataConverter.CompressedBinaryToString(data, SqlDbType.Float, 53, 0);

        Assert.Equal(expected.ToString(), result);
    }

    [Fact]
    public void Float_Expand_Returns_Eight_Bytes()
    {
        var (expanded, _) = CompressedDataConverter.Expand(new byte[] { 0xF0, 0x3F }, SqlDbType.Float);

        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0xF0, 0x3F }, expanded);
    }
    [Theory]
    [InlineData(new byte[] { 0x7F }, "-1")]
    [InlineData(new byte[] { 0x7E }, "-2")]
    [InlineData(new byte[] { 0x82 }, "2")]
    [InlineData(new byte[] { 0x7E, 0x98 }, "-360")]
    [InlineData(new byte[] { 0x81, 0xBB, 0x82 }, "113538")]
    public void Int_Decodes_Sign_Flipped_Big_Endian(byte[] data, string expected)
    {
        var result = CompressedDataConverter.CompressedBinaryToString(data, SqlDbType.Int, 10, 0);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(new byte[] { 0x7F }, "-1")]
    [InlineData(new byte[] { 0x7E }, "-2")]
    [InlineData(new byte[] { 0x81, 0x00, 0x00, 0x00, 0x00, 0x88, 0x00, 0x00 }, "72057594046840832")]
    public void BigInt_Decodes_Sign_Flipped_Big_Endian(byte[] data, string expected)
    {
        var result = CompressedDataConverter.CompressedBinaryToString(data, SqlDbType.BigInt, 19, 0);

        Assert.Equal(expected, result);
    }
}
