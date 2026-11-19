using System.Data;
using System.Globalization;
using System.Text;
using InternalsViewer.Query.Plans.Values;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Values;

[Trait("Category", "Unit")]
[Trait("Area", "Plans")]
public class AccessValueComparerTests
{
    [Theory]
    [InlineData("2019-12-31", -1)]
    [InlineData("2020-01-01", 0)]
    [InlineData("2020-06-15", 1)]
    public void A_Date_Column_Compares_Against_An_Iso_String_Literal(string columnDate, int expectedSign)
    {
        var date = DateOnly.Parse(columnDate, CultureInfo.InvariantCulture);

        var column = AccessValue.FromInteger(SqlDbType.Date, date.ToDateTime(TimeOnly.MinValue).Ticks);

        var literal = Literal("2020-01-01");

        Assert.Equal(expectedSign, Math.Sign(AccessValueComparer.Compare(column, literal)));

        Assert.Equal(-expectedSign, Math.Sign(AccessValueComparer.Compare(literal, column)));
    }

    [Theory]
    [InlineData("2023-12-27T16:51:07.9000000+00:00", 0)]
    [InlineData("2023-12-27T16:51:08+00:00", -1)]
    public void A_DateTimeOffset_Column_Compares_Against_An_Iso_String_Literal(string literalText, int expectedSign)
    {
        var instant = new DateTimeOffset(2023, 12, 27, 16, 51, 7, 900, TimeSpan.Zero);

        var column = AccessValue.FromInteger(SqlDbType.DateTimeOffset, instant.UtcTicks);

        var literal = Literal(literalText);

        Assert.Equal(expectedSign, Math.Sign(AccessValueComparer.Compare(column, literal)));
    }

    [Theory]
    [InlineData(19.99, "19.99", 0)]
    [InlineData(19.99, "19.98", 1)]
    [InlineData(0.1, "0.1", 0)]
    [InlineData(-12.5, "-12.49", -1)]
    public void A_Float_Column_Compares_Against_A_Decimal_By_Its_Shown_Value(double columnValue, string decimalText, int expectedSign)
    {
        var column = AccessValue.FromReal(SqlDbType.Float, columnValue);

        var literal = AccessValue.FromDecimal(SqlDbType.Decimal, decimal.Parse(decimalText, CultureInfo.InvariantCulture));

        Assert.Equal(expectedSign, Math.Sign(AccessValueComparer.Compare(column, literal)));

        Assert.Equal(-expectedSign, Math.Sign(AccessValueComparer.Compare(literal, column)));
    }

    private static AccessValue Literal(string text)
    {
        return AccessValue.FromBytes(SqlDbType.VarChar, Encoding.Latin1.GetBytes(text));
    }
}
