using System.Data;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Helpers;

public class SqlTypeHelpersTests
{
    [Theory]
    [InlineData(48, SqlDbType.TinyInt)]
    [InlineData(52, SqlDbType.SmallInt)]
    [InlineData(56, SqlDbType.Int)]
    [InlineData(127, SqlDbType.BigInt)]
    [InlineData(104, SqlDbType.Bit)]
    [InlineData(62, SqlDbType.Float)]
    [InlineData(59, SqlDbType.Real)]
    [InlineData(60, SqlDbType.Money)]
    [InlineData(122, SqlDbType.SmallMoney)]
    [InlineData(106, SqlDbType.Decimal)]
    [InlineData(36, SqlDbType.UniqueIdentifier)]
    [InlineData(61, SqlDbType.DateTime)]
    [InlineData(58, SqlDbType.SmallDateTime)]
    [InlineData(40, SqlDbType.Date)]
    [InlineData(41, SqlDbType.Time)]
    [InlineData(42, SqlDbType.DateTime2)]
    [InlineData(43, SqlDbType.DateTimeOffset)]
    [InlineData(167, SqlDbType.VarChar)]
    [InlineData(175, SqlDbType.Char)]
    [InlineData(231, SqlDbType.NVarChar)]
    [InlineData(239, SqlDbType.NChar)]
    [InlineData(165, SqlDbType.VarBinary)]
    [InlineData(173, SqlDbType.Binary)]
    [InlineData(35, SqlDbType.Text)]
    [InlineData(99, SqlDbType.NText)]
    [InlineData(34, SqlDbType.Image)]
    [InlineData(241, SqlDbType.Xml)]
    [InlineData(189, SqlDbType.Timestamp)]
    public void ToSqlType_Returns_Correct_SqlDbType(byte typeCode, SqlDbType expected)
    {
        Assert.Equal(expected, SqlTypeHelpers.ToSqlType(typeCode));
    }

    [Theory]
    [InlineData(SqlDbType.Int, true)]
    [InlineData(SqlDbType.SmallInt, true)]
    [InlineData(SqlDbType.TinyInt, true)]
    [InlineData(SqlDbType.BigInt, true)]
    [InlineData(SqlDbType.Bit, true)]
    [InlineData(SqlDbType.Float, true)]
    [InlineData(SqlDbType.Real, true)]
    [InlineData(SqlDbType.Money, true)]
    [InlineData(SqlDbType.SmallMoney, true)]
    [InlineData(SqlDbType.Decimal, true)]
    [InlineData(SqlDbType.UniqueIdentifier, true)]
    [InlineData(SqlDbType.DateTime, true)]
    [InlineData(SqlDbType.SmallDateTime, true)]
    [InlineData(SqlDbType.Date, true)]
    [InlineData(SqlDbType.Time, true)]
    [InlineData(SqlDbType.DateTime2, true)]
    [InlineData(SqlDbType.DateTimeOffset, true)]
    [InlineData(SqlDbType.Timestamp, true)]
    [InlineData(SqlDbType.VarChar, false)]
    [InlineData(SqlDbType.Char, false)]
    [InlineData(SqlDbType.NVarChar, false)]
    [InlineData(SqlDbType.NChar, false)]
    [InlineData(SqlDbType.VarBinary, false)]
    [InlineData(SqlDbType.Binary, false)]
    [InlineData(SqlDbType.Text, false)]
    [InlineData(SqlDbType.NText, false)]
    [InlineData(SqlDbType.Image, false)]
    [InlineData(SqlDbType.Xml, false)]
    public void IsFixedLength_Returns_Correct_Value(SqlDbType sqlType, bool expected)
    {
        Assert.Equal(expected, SqlTypeHelpers.IsFixedLength(sqlType));
    }

    [Theory]
    [InlineData(SqlDbType.VarChar, true)]
    [InlineData(SqlDbType.NVarChar, true)]
    [InlineData(SqlDbType.Int, false)]
    [InlineData(SqlDbType.BigInt, false)]
    public void IsVariableLength_Is_Inverse_Of_IsFixedLength(SqlDbType sqlType, bool expected)
    {
        Assert.Equal(expected, SqlTypeHelpers.IsVariableLength(sqlType));
        Assert.NotEqual(SqlTypeHelpers.IsFixedLength(sqlType), SqlTypeHelpers.IsVariableLength(sqlType));
    }
}
