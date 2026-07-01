using System.Data;
using InternalsViewer.Internals.Metadata.Internals;

namespace InternalsViewer.Internals.Tests.UnitTests.Metadata.Internals;

public class TypeInfoExtensionsTests
{
    [Theory]
    [InlineData(48, SqlDbType.TinyInt, 1, 1, 3, 0)]
    [InlineData(52, SqlDbType.SmallInt, 2, 2, 5, 0)]
    [InlineData(56, SqlDbType.Int, 4, 4, 10, 0)]
    [InlineData(127, SqlDbType.BigInt, 8, 8, 19, 0)]
    [InlineData(104, SqlDbType.Bit, 1, 1, 1, 0)]
    [InlineData(60, SqlDbType.Money, 8, 8, 19, 4)]
    [InlineData(122, SqlDbType.SmallMoney, 4, 4, 10, 4)]
    [InlineData(62, SqlDbType.Float, 8, 8, 53, 0)]
    [InlineData(59, SqlDbType.Real, 4, 4, 24, 0)]
    [InlineData(61, SqlDbType.DateTime, 8, 8, 23, 3)]
    [InlineData(58, SqlDbType.SmallDateTime, 4, 4, 16, 0)]
    [InlineData(40, SqlDbType.Date, 3, 3, 10, 0)]
    [InlineData(189, SqlDbType.Timestamp, 8, 8, 0, 0)]
    [InlineData(36, SqlDbType.UniqueIdentifier, 16, 16, 0, 0)]
    [InlineData(98, SqlDbType.Variant, 8016, 8016, 0, 0)]
    public void ToTypeInfo_Maps_Fixed_Types(int value,
                                            SqlDbType expectedType,
                                            short expectedMaxLength,
                                            short expectedMaxInRowLength,
                                            byte expectedPrecision,
                                            byte expectedScale)
    {
        var typeInfo = value.ToTypeInfo();

        Assert.Equal(expectedType, typeInfo.DataType);
        Assert.Equal(expectedMaxLength, typeInfo.MaxLength);
        Assert.Equal(expectedMaxInRowLength, typeInfo.MaxInRowLength);
        Assert.Equal(expectedPrecision, typeInfo.Precision);
        Assert.Equal(expectedScale, typeInfo.Scale);
    }

    [Theory]
    [InlineData(5, 0, 5)]
    [InlineData(9, 2, 5)]
    [InlineData(18, 4, 9)]
    [InlineData(28, 6, 13)]
    [InlineData(38, 10, 17)]
    public void ToTypeInfo_Decimal_Sets_Precision_Scale_And_Length(byte precision,
                                                                   byte scale,
                                                                   short expectedMaxLength)
    {
        // Decimal type byte 106, precision packed into bits 8-15, scale into bits 16-23.
        var value = 106 | (precision << 8) | (scale << 16);

        var typeInfo = value.ToTypeInfo();

        Assert.Equal(SqlDbType.Decimal, typeInfo.DataType);
        Assert.Equal(precision, typeInfo.Precision);
        Assert.Equal(scale, typeInfo.Scale);
        Assert.Equal(expectedMaxLength, typeInfo.MaxLength);
        Assert.Equal(expectedMaxLength, typeInfo.MaxInRowLength);
    }

    [Theory]
    [InlineData(0, 3, 8)]
    [InlineData(2, 3, 11)]
    [InlineData(3, 4, 12)]
    [InlineData(4, 4, 13)]
    [InlineData(7, 5, 16)]
    public void ToTypeInfo_Time_Sets_Scale_Length_And_Precision(byte scale,
                                                                short expectedMaxLength,
                                                                byte expectedPrecision)
    {
        // Time type byte 41, scale packed into bits 8-15.
        var value = 41 | (scale << 8);

        var typeInfo = value.ToTypeInfo();

        Assert.Equal(SqlDbType.Time, typeInfo.DataType);
        Assert.Equal(scale, typeInfo.Scale);
        Assert.Equal(expectedMaxLength, typeInfo.MaxLength);
        Assert.Equal(expectedPrecision, typeInfo.Precision);
    }

    [Theory]
    [InlineData(0, 6, 20)]
    [InlineData(2, 6, 22)]
    [InlineData(3, 7, 23)]
    [InlineData(4, 7, 24)]
    [InlineData(7, 8, 27)]
    public void ToTypeInfo_DateTime2_Sets_Scale_Length_And_Precision(byte scale,
                                                                     short expectedMaxLength,
                                                                     byte expectedPrecision)
    {
        // DateTime2 type byte 42, scale packed into bits 8-15.
        var value = 42 | (scale << 8);

        var typeInfo = value.ToTypeInfo();

        Assert.Equal(SqlDbType.DateTime2, typeInfo.DataType);
        Assert.Equal(scale, typeInfo.Scale);
        Assert.Equal(expectedMaxLength, typeInfo.MaxLength);
        Assert.Equal(expectedPrecision, typeInfo.Precision);
    }

    [Theory]
    [InlineData(0, 8, 26)]
    [InlineData(2, 8, 29)]
    [InlineData(3, 9, 30)]
    [InlineData(4, 9, 31)]
    [InlineData(7, 10, 34)]
    public void ToTypeInfo_DateTimeOffset_Sets_Scale_Length_And_Precision(byte scale,
                                                                          short expectedMaxLength,
                                                                          byte expectedPrecision)
    {
        // DateTimeOffset type byte 43, scale packed into bits 8-15.
        var value = 43 | (scale << 8);

        var typeInfo = value.ToTypeInfo();

        Assert.Equal(SqlDbType.DateTimeOffset, typeInfo.DataType);
        Assert.Equal(scale, typeInfo.Scale);
        Assert.Equal(expectedMaxLength, typeInfo.MaxLength);
        Assert.Equal(expectedPrecision, typeInfo.Precision);
    }

    [Theory]
    [InlineData(167, SqlDbType.VarChar)]
    [InlineData(175, SqlDbType.Char)]
    [InlineData(231, SqlDbType.NVarChar)]
    [InlineData(239, SqlDbType.NChar)]
    [InlineData(165, SqlDbType.VarBinary)]
    [InlineData(173, SqlDbType.Binary)]
    public void ToTypeInfo_Variable_Length_Sets_MaxLength_From_Value(int typeByte, SqlDbType expectedType)
    {
        // Max length packed into bits 8-23.
        var value = typeByte | (50 << 8);

        var typeInfo = value.ToTypeInfo();

        Assert.Equal(expectedType, typeInfo.DataType);
        Assert.Equal(50, typeInfo.MaxLength);
        Assert.Equal(50, typeInfo.MaxInRowLength);
    }

    [Fact]
    public void ToTypeInfo_Variable_Length_Max_Treats_Zero_Length_As_Unlimited()
    {
        // varchar(max) has a zero length, which maps to -1 with an 8000 in-row limit.
        var typeInfo = 167.ToTypeInfo();

        Assert.Equal(SqlDbType.VarChar, typeInfo.DataType);
        Assert.Equal(-1, typeInfo.MaxLength);
        Assert.Equal(8000, typeInfo.MaxInRowLength);
    }
}
