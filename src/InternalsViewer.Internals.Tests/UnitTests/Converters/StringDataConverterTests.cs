using System.Data;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Converters;

public class StringDataConverterTests
{
    [Theory]
    [InlineData("41", SqlDbType.Char, "A")]
    [InlineData(
        "41 41 41 41 41 41 41 41 41 41 41 41 41 41 41 41 41 41 41 41 42 42 42 42 42 42 42 42 42 42 42 42 42 42 42 42 42 42 42 42 43 43 43 43 43 43 43 43 43 43 43 43 43 43 43 43 43 43 43 43 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 ", 
        SqlDbType.Char, 
        "AAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBCCCCCCCCCCCCCCCCCCCC                                        ")]
    [InlineData("58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A",
        SqlDbType.VarChar, 
        "XXXXXXXXXXXXXXXXXXXXYYYYYYYYYYYYYYYYYYYYZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("42 00", SqlDbType.NChar, "B")]
    [InlineData(
        "41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 41 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 42 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 43 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 20 00 ", 
        SqlDbType.Char, 
        "AAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBCCCCCCCCCCCCCCCCCCCC                                        ")]
    [InlineData("58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 59 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A 5A", 
        SqlDbType.VarChar,
        "XXXXXXXXXXXXXXXXXXXXYYYYYYYYYYYYYYYYYYYYZZZZZZZZZZZZZZZZZZZZ")]
    public void Can_Convert_Binary_To_String(string bytesString, SqlDbType dataType, string expected)
    {
        // Arrange
        var bytes = bytesString.ToByteArray();

        var result = DataConverter.BinaryToString(bytes, dataType);

        Assert.Equal(expected, result);
    }
}