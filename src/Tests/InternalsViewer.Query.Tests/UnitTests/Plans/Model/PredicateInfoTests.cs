using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Query.Plans.Joins;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Model;

[Trait("Category", "Unit")]
public class PredicateInfoTests
{
    [Fact]
    public void Key_Columns_Equal_To_The_Outer_Row_Are_A_Correlated_Equality()
    {
        var info = new PredicateInfo { CorrelatedSeekColumns = [Column("OrderId"), Column("LineId")] };

        Assert.True(info.IsCorrelatedEquality);
    }

    [Fact]
    public void A_Range_Bound_From_The_Outer_Row_Is_Not_A_Correlated_Equality()
    {
        var info = new PredicateInfo { CorrelatedSeekColumns = [Column("Id", ComparisonOperator.GreaterThan)] };

        Assert.False(info.IsCorrelatedEquality);
    }

    [Fact]
    public void Several_Ranges_On_One_Column_Are_Not_A_Correlated_Equality()
    {
        var info = new PredicateInfo { CorrelatedSeekColumns = [Column("Id"), Column("Id")] };

        Assert.False(info.IsCorrelatedEquality);
    }

    [Fact]
    public void A_Seek_With_Constant_Ranges_Is_Not_A_Correlated_Equality()
    {
        var info = new PredicateInfo { SeekBounds = [SeekBounds.All], CorrelatedSeekColumns = [Column("Id")] };

        Assert.False(info.IsCorrelatedEquality);
    }

    [Fact]
    public void A_Seek_Without_Correlation_Is_Not_A_Correlated_Equality()
        => Assert.False(new PredicateInfo().IsCorrelatedEquality);

    private static CorrelatedSeekColumn Column(string name, ComparisonOperator comparison = ComparisonOperator.Equal)
        => new(name, "Outer", name, comparison);
}
