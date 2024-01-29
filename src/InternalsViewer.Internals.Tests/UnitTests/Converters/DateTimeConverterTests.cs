using System.Data;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Converters;

public class DateTimeConverterTests
{
    [Fact]
    public void Can_Convert_DateTime_To_String()
    {
        // Arrange
        var bytes = "98 53 F4 00 E5 B0 00 00".ToByteArray();

        // Act
        var result = DataConverter.BinaryToString(bytes, SqlDbType.DateTime);

        // Assert
        Assert.Equal("12/27/2023 14:49:33", result);
    }

    [Fact]
    public void Can_Convert_SmallDateTime_To_String()
    {
        // Arrange
        var bytes = "7A 03 E5 B0".ToByteArray();

        // Act
        var result = DataConverter.BinaryToString(bytes, SqlDbType.SmallDateTime);

        // Assert
        Assert.Equal("12/27/2023 14:50:00", result);
    }

    [Fact]
    public void Can_Convert_Date_To_String()
    {
        // Arrange
        var bytes = "40 46 0B".ToByteArray();

        // Act
        var result = DataConverter.BinaryToString(bytes, SqlDbType.Date);

        // Assert
        Assert.Equal("27/12/2023", result);
    }

    [Theory]
    [InlineData("10 E7 00", 0, "16:25:52")]
    [InlineData("9C 06 09", 1, "16:25:51.6")]
    [InlineData("1B 42 5A", 2, "16:25:51.63")]
    [InlineData("0E 95 86 03", 3, "16:25:51.632")]
    [InlineData("8C D2 41 23", 4, "16:25:51.6280")]
    [InlineData("78 39 92 60 01", 5, "16:25:51.63200")]
    [InlineData("B0 3E B6 C5 0D", 6, "16:25:51.632000")]
    [InlineData("E0 72 1E B9 89", 7, "16:25:51.6240000")]
    public void Can_Convert_Time_To_String(string bytesString, byte scale, string expected)
    {
        // Arrange
        var bytes = bytesString.ToByteArray();

        // Act
        var result = DataConverter.BinaryToString(bytes, SqlDbType.Time, 0, scale);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("FC EC 00 40 46 0B", 0, "2023-12-27 16:51:08")]
    [InlineData("D7 41 09 40 46 0B ", 1, "2023-12-27 16:51:07.9")]
    [InlineData("66 92 5C 40 46 0B ", 2, "2023-12-27 16:51:07.90")]
    [InlineData("FC B7 9D 03 40 46 0B", 3, "2023-12-27 16:51:07.900")]
    [InlineData("D8 2F 29 24 40 46 0B", 4, "2023-12-27 16:51:07.9000")]
    [InlineData("70 DE 9B 69 01 40 46 0B ", 5, "2023-12-27 16:51:07.89600")]
    [InlineData("60 B0 16 20 0E 40 46 0B", 6, "2023-12-27 16:51:07.904000")]
    [InlineData("C0 E3 E2 40 8D 40 46 0B", 7, "2023-12-27 16:51:07.9000000")]
    public void Can_Convert_DateTime2_To_String(string bytesString, byte scale, string expected)
    {
        // Arrange
        var bytes = bytesString.ToByteArray();

        // Act
        var result = DataConverter.BinaryToString(bytes, SqlDbType.DateTime2, 0, scale);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("C0 E3 E2 40 8D 40 46 0B 00 00", 7, "2023-12-27 16:51:07.9000000 +00:00")]
    public void Can_Convert_DateTimeOffset_To_String(string bytesString, byte scale, string expected)
    {
        // Arrange
        var bytes = bytesString.ToByteArray();

        // Act
        var result = DataConverter.BinaryToString(bytes, SqlDbType.DateTimeOffset, 0, scale);

        // Assert
        Assert.Equal(expected, result);
    }
}