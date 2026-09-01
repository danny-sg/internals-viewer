using System.Collections.Immutable;
using System.Data;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Execution.Tests.UnitTests.AccessPaths;

[Trait("Category", "Unit")]
[Trait("Area", "AccessPaths")]
public class CompressedDataFilterTests
{
    [Fact]
    public void A_Single_Comparison_Is_A_Plain_Conjunction()
    {
        var predicate = Comparison("Id", 1900000);

        Assert.True(CompressedDataFilter.IsPlainConjunction(predicate));

        Assert.Equal([predicate], CompressedDataFilter.Conjunctions(predicate));
    }

    [Fact]
    public void An_And_Of_Comparisons_Is_A_Plain_Conjunction()
    {
        var left = Comparison("Id", 1900000);

        var right = Comparison("Spread", 939);

        var predicate = new AccessPredicate.And([left, right]);

        Assert.True(CompressedDataFilter.IsPlainConjunction(predicate));

        Assert.Equal([left, right], CompressedDataFilter.Conjunctions(predicate));
    }

    [Fact]
    public void An_Or_Is_Not_A_Plain_Conjunction_And_Yields_Nothing()
    {
        var predicate = new AccessPredicate.Or([Comparison("Id", 1900000), Comparison("Spread", 939)]);

        Assert.False(CompressedDataFilter.IsPlainConjunction(predicate));

        Assert.Empty(CompressedDataFilter.Conjunctions(predicate));
    }

    [Fact]
    public void An_And_Holding_An_Or_Is_Not_Plain_But_Still_Yields_Its_Comparisons()
    {
        var pushable = Comparison("Id", 1900000);

        var predicate = new AccessPredicate.And(
        [
            pushable,
            new AccessPredicate.Or([Comparison("Spread", 939), Comparison("Spread", 940)])
        ]);

        Assert.False(CompressedDataFilter.IsPlainConjunction(predicate));

        Assert.Equal([pushable], CompressedDataFilter.Conjunctions(predicate));
    }

    [Fact]
    public void A_Nested_And_Flattens_To_Every_Comparison()
    {
        var first = Comparison("Id", 1);

        var second = Comparison("Spread", 2);

        var third = Comparison("Filler", 3);

        var predicate = new AccessPredicate.And([first, new AccessPredicate.And([second, third])]);

        Assert.True(CompressedDataFilter.IsPlainConjunction(predicate));

        Assert.Equal([first, second, third], CompressedDataFilter.Conjunctions(predicate));
    }

    private static AccessPredicate.Comparison Comparison(string column, long value)
        => new(new AccessExpression.Column(-1, column),
               ComparisonOperator.Equal,
               new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.BigInt, value)));
}
