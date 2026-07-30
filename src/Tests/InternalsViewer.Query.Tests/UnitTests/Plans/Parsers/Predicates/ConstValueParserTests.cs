using System.Data;
using System.Text;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Query.Plans.Parsers.Predicates;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Parsers.Predicates;

public class ConstValueParserTests
{
    [Theory]
    [InlineData("NULL")]
    public void Unparseable_Or_Null_Literals_Become_Null(string literal)
    {
        Assert.Equal(AccessValueType.Null, ConstValueParser.Parse(literal).Type);
    }

    [Fact]
    public void Bracketed_Literal_Is_Unwrapped()
    {
        var value = ConstValueParser.Parse("(42)");

        Assert.Equal(AccessValueType.Integer, value.Type);
        Assert.Equal(42, value.Numeric);
    }

    [Fact]
    public void Empty_Literal_Becomes_Null()
    {
        Assert.Equal(AccessValueType.Null, ConstValueParser.Parse(null).Type);
        Assert.Equal(AccessValueType.Null, ConstValueParser.Parse("  ").Type);
    }

    [Theory]
    [InlineData("42", 42L)]
    [InlineData("-7", -7L)]
    [InlineData("0", 0L)]
    public void Integer_Literals_Are_Exact(string literal, long expected)
    {
        var value = ConstValueParser.Parse(literal);

        Assert.Equal(AccessValueType.Integer, value.Type);
        Assert.Equal(expected, value.Numeric);
    }

    [Fact]
    public void Decimal_Literal_Keeps_Exact_Precision()
    {
        var value = ConstValueParser.Parse("19.99");

        Assert.Equal(AccessValueType.Decimal, value.Type);
        Assert.Equal(19.99m, value.ToDecimal());
    }

    [Fact]
    public void Exponent_Literal_Is_Real()
    {
        var value = ConstValueParser.Parse("1.5e3");

        Assert.Equal(AccessValueType.Real, value.Type);
    }

    [Fact]
    public void Quoted_String_Is_Unwrapped()
    {
        var value = ConstValueParser.Parse("'Sales'");

        Assert.Equal(AccessValueType.Bytes, value.Type);
        Assert.Equal(SqlDbType.VarChar, value.DataType);
        Assert.Equal("Sales", Encoding.ASCII.GetString(value.Data.ToArray()));
    }

    [Fact]
    public void Unicode_String_Uses_The_Unicode_Type()
    {
        var value = ConstValueParser.Parse("N'Sales'");

        Assert.Equal(SqlDbType.NVarChar, value.DataType);
        Assert.Equal("Sales", Encoding.Unicode.GetString(value.Data.ToArray()));
    }

    [Fact]
    public void Doubled_Quotes_Become_A_Single_Quote()
    {
        var value = ConstValueParser.Parse("'O''Brien'");

        Assert.Equal("O'Brien", Encoding.ASCII.GetString(value.Data.ToArray()));
    }

    [Fact]
    public void Empty_String_Literal_Is_An_Empty_Value()
    {
        var value = ConstValueParser.Parse("''");

        Assert.Equal(AccessValueType.Bytes, value.Type);
        Assert.Empty(value.Data.ToArray());
    }

    [Fact]
    public void Binary_Literal_Is_Decoded()
    {
        var value = ConstValueParser.Parse("0x01FF00");

        Assert.Equal(AccessValueType.Bytes, value.Type);
        Assert.Equal([0x01, 0xFF, 0x00], value.Data.ToArray());
    }

    [Fact]
    public void Odd_Length_Binary_Literal_Is_Rejected()
    {
        Assert.Equal(AccessValueType.Null, ConstValueParser.Parse("0x1FF").Type);
    }
}
