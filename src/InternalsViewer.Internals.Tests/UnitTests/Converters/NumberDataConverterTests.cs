using System.Data;
using InternalsViewer.Internals.Converters;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Converters;

public class NumberDataConverterTests
{
    [Theory]
    [InlineData("38", SqlDbType.TinyInt, "56")]
    [InlineData("DF", SqlDbType.TinyInt, "223")]
    [InlineData("8E", SqlDbType.TinyInt, "142")]
    [InlineData("5C 73", SqlDbType.SmallInt, "29532")]
    [InlineData("2F 08", SqlDbType.SmallInt, "2095")]
    [InlineData("51 74", SqlDbType.SmallInt, "29777")]
    [InlineData("D6 00 00 00", SqlDbType.Int, "214")]
    [InlineData("A3 43 AC 56", SqlDbType.Int, "1454130083")]
    [InlineData("F3 F6 05 E0", SqlDbType.Int, "-536480013")]
    [InlineData("78 4C 0C 29", SqlDbType.Int, "688671864")]
    [InlineData("CB E1 60 92 50 D4 9F D6", SqlDbType.BigInt, "-2981430985777684021")]
    [InlineData("A1 CC 64 4D BE 71 B6 8E", SqlDbType.BigInt, "-8163212212406268767")]
    [InlineData("70 61 B3 55 7D 50 45 BC", SqlDbType.BigInt, "-4880406121947111056")]
    [InlineData("63 6A 35 B8 24 54 8F 3B", SqlDbType.BigInt, "4291741486593436259")]
    public void Can_Convert_Int_Type_Number_From_Binary_To_String(string bytesString, SqlDbType dataType, string expected)
    {
        // Arrange
        var bytes = bytesString.ToByteArray();

        // Act
        var result = DataConverter.BinaryToString(bytes, dataType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("01 56 FC 00 00", 5, 0, "64598")]
    [InlineData("01 DA 26 01 00", 5, 0, "75482")]

    [InlineData("01 B3 A3 00 00", 5, 2, "419.07")]
    [InlineData("01 01 4F 01 00", 5, 2, "857.61")]

    [InlineData("01 86 7A F3 1A", 9, 0, "452164230")]
    [InlineData("01 24 13 33 14", 9, 0, "338891556")]

    [InlineData("01 00 C0 0B 30", 9, 4, "80607.6416")]
    [InlineData("01 1A A7 79 0E", 9, 4, "24285.3658")]

    [InlineData("00 B5 F7 9B 47 B0 B2 00 09", 18, 0, "-648714816526743477")]
    [InlineData("00 44 5F FB 26 4F A5 0B 0B", 18, 0, "-795911518536032068")]

    [InlineData("01 F0 8C DD 71 31 2A D8 0B", 18, 2, "8534785212388180.32")]
    [InlineData("01 36 3C 0D 0D A7 E9 C4 05", 18, 2, "4157139693127096.86")]

    [InlineData("00 D5 22 53 52 9E BC BA 26 00 00 00 00", 28, 0, "-2790750307281478357")]
    [InlineData("01 4A 64 69 04 14 62 41 3C 00 00 00 00", 28, 0, "4341859353874752586")]

    [InlineData("01 52 A9 93 C2 CD B1 F4 23 00 00 00 00", 28, 10, "259089118.2935746898")]
    [InlineData("01 ED 45 4B 0E A2 3C E0 07 00 00 00 00", 28, 10, "56752021.9770865133")]

    [InlineData("00 35 BF 97 A0 2E 19 C6 45 00 00 00 00 00 00 00 00", 32, 4, "-502773372205922.6933")]
    [InlineData("01 D0 07 D5 45 57 4E A3 1E 00 00 00 00 00 00 00 00", 32, 4, "220769437908238.5360")]

    [InlineData("00 9A 8E EA 1F B2 F2 07 40 00 00 00 00 00 00 00 00", 32, 8, "-46139231901.17928602")]
    [InlineData("00 95 FF CC 75 1E C0 AD 46 00 00 00 00 00 00 00 00", 32, 8, "-50929379906.83819925")]
    public void Can_Convert_Decimal_Type_Number_From_Binary_To_String(string bytesString, byte precision, byte scale, string expected)
    {
        // Arrange
        var bytes = bytesString.ToByteArray(); ;

        // Act
        var result = DataConverter.BinaryToString(bytes, SqlDbType.Decimal, precision, scale);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("48 85 47 45", SqlDbType.Real, "3192.33")]
    [InlineData("23 BC 3D 08 A9 F0 A8 40", SqlDbType.Float, "3192.330141")]
    public void Can_Convert_Float_Type_Number_From_Binary_To_String(string bytesString, SqlDbType dataType, string expected)
    {
        // Arrange
        var bytes = bytesString.ToByteArray(); 

        // Act
        var result = DataConverter.BinaryToString(bytes, dataType);

        // Assert
        Assert.Equal(expected, result);
    }


    [Theory]
    [InlineData("FE 48 82 71", SqlDbType.SmallMoney, "190436.3774")]
    [InlineData("56 BD 25 E8 0D D7 F4 B5", SqlDbType.Money, "-533540320379786.1034")]
    public void Can_Convert_Money_Type_Number_From_Binary_To_String(string bytesString, SqlDbType dataType, string expected)
    {
        // Arrange
        var bytes = bytesString.ToByteArray();

        // Act
        var result = DataConverter.BinaryToString(bytes, dataType);

        // Assert
        Assert.Equal(expected, result);
    }
}