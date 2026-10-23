using InternalsViewer.Internals.Converters.Decoder;

namespace InternalsViewer.Internals.Tests.UnitTests.Converters;

[Trait("Category", "Unit")]
[Trait("Area", "Converters")]
public class DataDecoderTests
{
    [Fact]
    public void Decode_Single_Byte_Includes_TinyInt_And_Binary()
    {
        // 0x01 = 1 as TinyInt, binary "00000001"
        var results = DataDecoder.Decode("01");

        Assert.Contains(results, r => r.DataType == "TinyInt" && r.Value == "1");
        Assert.Contains(results, r => r.DataType == "binary");
    }

    [Fact]
    public void Decode_Two_Bytes_Includes_SmallInt()
    {
        // 0x0100 = 256 as SmallInt (little-endian)
        var results = DataDecoder.Decode("0001");

        Assert.Contains(results, r => r.DataType == "SmallInt" && r.Value == "256");
    }

    [Fact]
    public void Decode_Four_Bytes_Includes_Int()
    {
        // 0x01000000 = 1 as Int (little-endian)
        var results = DataDecoder.Decode("01000000");

        Assert.Contains(results, r => r.DataType == "Int" && r.Value == "1");
    }

    [Fact]
    public void Decode_Eight_Bytes_Includes_BigInt()
    {
        // little-endian 1 as BigInt
        var results = DataDecoder.Decode("0100000000000000");

        Assert.Contains(results, r => r.DataType == "BigInt" && r.Value == "1");
    }

    [Theory]
    [InlineData("FFFF", "SmallInt", "-1", "UInt16", "65535")]
    [InlineData("FFFFFFFF", "Int", "-1", "UInt32", "4294967295")]
    [InlineData("FFFFFFFFFFFFFFFF", "BigInt", "-1", "UInt64", "18446744073709551615")]
    public void Decode_Includes_The_Unsigned_Reading_After_The_Signed_One(string hex,
                                                                         string signedType,
                                                                         string signedValue,
                                                                         string unsignedType,
                                                                         string unsignedValue)
    {
        var results = DataDecoder.Decode(hex);

        var signed = results.FindIndex(r => r.DataType == signedType && r.Value == signedValue);

        Assert.True(signed >= 0);

        Assert.Equal(new DecodeResult(unsignedType, unsignedValue), results[signed + 1]);
    }

    [Theory]
    [InlineData("010000", "1", "1")]
    [InlineData("FFFF7F", "8388607", "8388607")]
    [InlineData("000080", "-8388608", "8388608")]
    [InlineData("FFFFFF", "-1", "16777215")]
    public void Decode_Three_Bytes_Sign_Extends_Int_And_Zero_Pads_UInt32(string hex, string signed, string unsigned)
    {
        var results = DataDecoder.Decode(hex);

        Assert.Contains(results, r => r.DataType == "Int" && r.Value == signed);

        Assert.Contains(results, r => r.DataType == "UInt32" && r.Value == unsigned);
    }

    [Theory]
    [InlineData("0100000000", "1", "1")]
    [InlineData("0000000080", "-549755813888", "549755813888")]
    [InlineData("FFFFFFFFFF", "-1", "1099511627775")]
    [InlineData("FFFFFFFFFF7F", "140737488355327", "140737488355327")]
    [InlineData("FFFFFFFFFFFF", "-1", "281474976710655")]
    [InlineData("00000000000080", "-36028797018963968", "36028797018963968")]
    [InlineData("FFFFFFFFFFFFFF", "-1", "72057594037927935")]
    public void Decode_Five_To_Seven_Bytes_Sign_Extends_BigInt_And_Zero_Pads_UInt64(string hex, string signed, string unsigned)
    {
        var results = DataDecoder.Decode(hex);

        Assert.Contains(results, r => r.DataType == "BigInt" && r.Value == signed);

        Assert.Contains(results, r => r.DataType == "UInt64" && r.Value == unsigned);
    }

    [Fact]
    public void Decode_Single_Byte_Has_No_Unsigned_Reading()
    {
        var results = DataDecoder.Decode("FF");

        Assert.DoesNotContain(results, r => r.DataType.StartsWith("UInt"));
    }

    [Fact]
    public void Decode_Single_Byte_Zero_Has_Binary_All_Zeros()
    {
        var results = DataDecoder.Decode("00");

        var binaryResult = results.FirstOrDefault(r => r.DataType == "binary");

        Assert.NotNull(binaryResult);
        Assert.Equal("00000000", binaryResult.Value);
    }

    [Fact]
    public void Decode_Single_Byte_Full_Has_Binary_All_Ones()
    {
        var results = DataDecoder.Decode("FF");

        var binaryResult = results.FirstOrDefault(r => r.DataType == "binary");

        Assert.NotNull(binaryResult);
        Assert.Equal("11111111", binaryResult.Value);
    }

    [Fact]
    public void Decode_With_Spaces_In_Input_Is_Handled()
    {
        // DataDecoder.Decode calls CleanHex which strips spaces
        var results = DataDecoder.Decode("01 00 00 00");

        Assert.Contains(results, r => r.DataType == "Int" && r.Value == "1");
    }

    [Fact]
    public void Decode_Ascii_Bytes_Includes_VarChar()
    {
        // 0x41 = 'A'
        var results = DataDecoder.Decode("41");

        Assert.Contains(results, r => r.DataType == "VarChar");
    }

    [Fact]
    public void Decode_Returns_NonEmpty_List()
    {
        var results = DataDecoder.Decode("7B000000");

        Assert.NotEmpty(results);
    }
}
