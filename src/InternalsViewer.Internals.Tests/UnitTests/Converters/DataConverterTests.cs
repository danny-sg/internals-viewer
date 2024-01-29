using System.Data;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Converters;

public class DataConverterTests
{
    [Fact]
    public void Can_Convert_Varbinary_To_String()
    {
        var bytes = "98 53 F4 00 E5 B0 00 00".ToByteArray();

        var result = DataConverter.BinaryToString(bytes, SqlDbType.VarBinary);

        Assert.Equal("0x9853F400E5B00000", result);
    }

    [Fact]
    public void Can_Convert_Binary_To_String()
    {
        var bytes = "98 53 F4 00 E5 B0 00 00".ToByteArray();

        var result = DataConverter.BinaryToString(bytes, SqlDbType.Binary);

        Assert.Equal("0x9853F400E5B00000", result);
    }

    [Fact]
    public void Can_Convert_UniqueIdentifier_To_String()
    {
        var bytes = "98 41 25 75 8E EC 43 4E B7 B3 26 E6 3C 6E 50 9C".ToByteArray();

        var result = DataConverter.BinaryToString(bytes, SqlDbType.UniqueIdentifier);

        Assert.Equal("75254198-EC8E-4E43-B7B3-26E63C6E509C", result);
    }

    [Theory]
    [InlineData("FF", "True", 0)]
    [InlineData("FF", "True", 1)]
    [InlineData("FF", "True", 2)]
    [InlineData("FF", "True", 3)]
    [InlineData("FF", "True", 4)]
    [InlineData("FF", "True", 5)]
    [InlineData("FF", "True", 6)]
    [InlineData("FF", "True", 7)]
    [InlineData("00", "False", 0)]
    [InlineData("00", "False", 1)]
    [InlineData("00", "False", 2)]
    [InlineData("00", "False", 3)]
    [InlineData("00", "False", 4)]
    [InlineData("00", "False", 5)]
    [InlineData("00", "False", 6)]
    [InlineData("00", "False", 7)]
    [InlineData("01", "True", 0)]
    [InlineData("01", "False", 1)]
    [InlineData("02", "False", 0)]
    [InlineData("02", "True", 1)]
    public void Can_Convert_Bit_To_String(string bytesString, string expected, short bitPosition)
    {
        var bytes = bytesString.ToByteArray();

        var result = DataConverter.BinaryToString(bytes, SqlDbType.Bit, 0, 0, bitPosition);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2B 01 07 20 AB 4C 1B 90 40 46 0B 00 00", "2023-12-27 17:11:33.3320000 +00:00", "datetimeoffset")]
    [InlineData("3D 01 3F 53 1B 01 E5 B0 00 00", "12/27/2023 17:11:33", "datetime")]
    [InlineData("3A 01 08 04 E5 B0", "12/27/2023 17:12:00", "smalldatetime")]
    [InlineData("28 01 40 46 0B", "27/12/2023", "date")]
    [InlineData("29 01 07 20 AB 4C 1B 90", "17:11:33.3320000", "time")]
    [InlineData("3E 01 00 00 00 00 00 D0 74 40", "333", "float")]
    [InlineData("3B 01 00 80 A6 43", "333", "real")]
    [InlineData("2A 01 07 20 AB 4C 1B 90 40 46 0B", "2023-12-27 17:11:33.3320000", "datetime2(7)")]
    [InlineData("2A 01 01 8B F1 09 40 46 0B", "2023-12-27 18:06:05.9", "datetime2(1)")]
    [InlineData("2A 01 00 8E FE 00 40 46 0B", "2023-12-27 18:06:06", "datetime2(0)")]
    [InlineData("6A 01 12 00 01 4D 01 00 00", "333", "decimal")]
    [InlineData("3C 01 D0 CF 32 00 00 00 00 00", "333", "money")]
    [InlineData("7A 01 D0 CF 32 00", "333", "smallmoney")]
    [InlineData("7F 01 0F 27 00 00 00 00 00 00", "9999", "bigint")]
    [InlineData("38 01 0F 27 00 00", "9999", "int")]
    [InlineData("34 01 0F 27", "9999", "smallint")]
    [InlineData("34 01 63 00 ", "99", "tinyint")]
    [InlineData("68 01 01", "True", "bit")]
    public void Can_Convert_SqlVariant_To_String(string bytesString, string expected, string type)
    {
        var bytes = bytesString.ToByteArray();

        var result = DataConverter.BinaryToString(bytes, SqlDbType.Variant);

        Assert.Equal(expected, result);
    }
}