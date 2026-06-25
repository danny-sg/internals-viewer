using InternalsViewer.Internals.Converters.Decoder;

namespace InternalsViewer.Internals.Tests.UnitTests.Converters;

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
