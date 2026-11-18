using System.Data;
using System.Globalization;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Execution.Tests.UnitTests.AccessPaths.Values;

[Trait("Category", "Unit")]
[Trait("Area", "AccessPaths")]
public class AccessValueComparerTests
{
    [Theory]
    [InlineData("2019-12-31", -1)]
    [InlineData("2020-01-01", 0)]
    [InlineData("2020-06-15", 1)]
    public void A_Date_Column_Compares_Against_An_Iso_String_Literal(string columnDate, int expectedSign)
    {
        var column = AccessValueFactory.FromObject(SqlDbType.Date, DateOnly.Parse(columnDate, CultureInfo.InvariantCulture));

        var literal = AccessValueFactory.FromText(SqlDbType.VarChar, "2020-01-01");

        Assert.Equal(expectedSign, Math.Sign(AccessValueComparer.Compare(column, literal)));

        Assert.Equal(-expectedSign, Math.Sign(AccessValueComparer.Compare(literal, column)));
    }

    [Fact]
    public void A_Date_Value_Is_Built_As_A_Tick_Count_Not_A_String()
    {
        var value = AccessValueFactory.FromObject(SqlDbType.Date, new DateOnly(2020, 1, 1));

        Assert.Equal(AccessValueType.Integer, value.Type);

        Assert.Equal(new DateTime(2020, 1, 1).Ticks, value.Numeric);
    }

    [Theory]
    [InlineData("2023-12-27T16:51:07.9000000+00:00", 0)]
    [InlineData("2023-12-27T16:51:08+00:00", -1)]
    public void A_DateTimeOffset_Column_Compares_Against_An_Iso_String_Literal(string literalText, int expectedSign)
    {
        var instant = new DateTimeOffset(2023, 12, 27, 16, 51, 7, 900, TimeSpan.Zero);

        var column = AccessValueFactory.FromObject(SqlDbType.DateTimeOffset, instant);

        var literal = AccessValueFactory.FromText(SqlDbType.VarChar, literalText);

        Assert.Equal(expectedSign, Math.Sign(AccessValueComparer.Compare(column, literal)));
    }
}
