using System.Data;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Converters;

/// <summary>
/// Round-trip tests: encode a value with DataEncoders, decode the resulting hex string with
/// DataConverter.BinaryToString, and assert the original value is recovered.
/// </summary>
public class DataEncodersTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void EncodeInt32_RoundTrips(int value)
    {
        var hex = DataEncoders.EncodeInt32(value);
        var bytes = hex.ToByteArray();

        Assert.Equal(value, BitConverter.ToInt32(bytes, 0));
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)1)]
    [InlineData((short)-1)]
    [InlineData(short.MaxValue)]
    [InlineData(short.MinValue)]
    public void EncodeInt16_RoundTrips(short value)
    {
        var hex = DataEncoders.EncodeInt16(value);
        var bytes = hex.ToByteArray();

        Assert.Equal(value, BitConverter.ToInt16(bytes, 0));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void EncodeInt64_RoundTrips(long value)
    {
        var hex = DataEncoders.EncodeInt64(value);
        var bytes = hex.ToByteArray();

        Assert.Equal(value, BitConverter.ToInt64(bytes, 0));
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(1.0f)]
    [InlineData(-1.5f)]
    [InlineData(3.14159f)]
    public void EncodeReal_RoundTrips(float value)
    {
        var hex = DataEncoders.EncodeReal(value);
        var bytes = hex.ToByteArray();

        Assert.Equal(value, BitConverter.ToSingle(bytes, 0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-1.5)]
    [InlineData(3.141592653589793)]
    public void EncodeFloat_RoundTrips(double value)
    {
        var hex = DataEncoders.EncodeFloat(value);
        var bytes = hex.ToByteArray();

        Assert.Equal(value, BitConverter.ToDouble(bytes, 0));
    }

    [Theory]
    [InlineData("0.0000")]
    [InlineData("1.0000")]
    [InlineData("-1.5000")]
    [InlineData("1234.5678")]
    public void EncodeMoney_Decodes_To_Expected_Value(string decimalString)
    {
        var value = decimal.Parse(decimalString);

        var hex = DataEncoders.EncodeMoney(value);
        var bytes = hex.ToByteArray();

        var decoded = DataConverter.BinaryToString(bytes, SqlDbType.Money);

        Assert.Equal(value, decimal.Parse(decoded));
    }

    [Theory]
    [InlineData("0.0000")]
    [InlineData("1.0000")]
    [InlineData("-1.5000")]
    [InlineData("214748.3647")]
    public void EncodeSmallMoney_Decodes_To_Expected_Value(string decimalString)
    {
        var value = decimal.Parse(decimalString);

        var hex = DataEncoders.EncodeSmallMoney(value);
        var bytes = hex.ToByteArray();

        var decoded = DataConverter.BinaryToString(bytes, SqlDbType.SmallMoney);

        Assert.Equal(value, decimal.Parse(decoded));
    }

    [Fact]
    public void EncodeInt32_Produces_Hex_With_Spaces()
    {
        var hex = DataEncoders.EncodeInt32(1);

        // Format: "XX XX XX XX"
        Assert.Contains(" ", hex);
        Assert.Equal(11, hex.Length);
    }

    [Fact]
    public void EncodeDateTime_Returns_Two_Parts()
    {
        var dt = new DateTime(2024, 6, 15, 12, 0, 0);

        var parts = DataEncoders.EncodeDateTime(dt);

        Assert.Equal(2, parts.Length);
    }

    [Fact]
    public void EncodeSmallDateTime_Returns_Two_Parts()
    {
        var dt = new DateTime(2024, 6, 15, 12, 30, 0);

        var parts = DataEncoders.EncodeSmallDateTime(dt);

        Assert.Equal(2, parts.Length);
    }
}
