using System.Data;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;

namespace InternalsViewer.Internals.Tests.UnitTests.DataAccess.AccessPaths;

public class PredicateWriterTests
{
    [Fact]
    public void Comparison_Is_Written_As_Sql()
    {
        var predicate = new AccessPredicate.Comparison(Column(0, "OrderId"),
                                                       ComparisonOperator.GreaterThanOrEqual,
                                                       Constant(100));

        Assert.Equal("OrderId >= 100", Text(predicate));
    }

    [Fact]
    public void Column_Token_Carries_Its_Ordinal()
    {
        var predicate = new AccessPredicate.Comparison(Column(3, "OrderId"),
                                                       ComparisonOperator.Equal,
                                                       Constant(1));

        var column = PredicateWriter.Write(predicate)
                                    .First(t => t.Kind == PredicateTokenKind.Column);

        Assert.Equal("Ordinal 3", column.Description);
    }

    [Fact]
    public void Nested_Disjunction_Is_Bracketed()
    {
        var left = new AccessPredicate.Comparison(Column(0, "A"), ComparisonOperator.Equal, Constant(1));
        var right = new AccessPredicate.Comparison(Column(1, "B"), ComparisonOperator.Equal, Constant(2));
        var other = new AccessPredicate.Comparison(Column(2, "C"), ComparisonOperator.Equal, Constant(3));

        var predicate = new AccessPredicate.And([new AccessPredicate.Or([left, right]), other]);

        Assert.Equal("(A = 1 OR B = 2) AND C = 3", Text(predicate));
    }

    [Fact]
    public void Null_Test_And_Negation_Are_Written()
    {
        var predicate = new AccessPredicate.Not(new AccessPredicate.IsNull(Column(0, "Name")));

        Assert.Equal("NOT Name IS NULL", Text(predicate));
    }

    [Fact]
    public void In_List_Is_Written()
    {
        var predicate = new AccessPredicate.In(Column(0, "Id"), [Constant(1), Constant(2), Constant(3)]);

        Assert.Equal("Id IN (1, 2, 3)", Text(predicate));
    }

    [Fact]
    public void String_Constant_Is_Quoted()
    {
        var value = AccessValue.FromBytes(SqlDbType.VarChar, "O'Brien"u8.ToArray());

        var predicate = new AccessPredicate.Comparison(Column(0, "Name"),
                                                       ComparisonOperator.Equal,
                                                       new AccessExpression.Constant(value));

        Assert.Equal("Name = 'O''Brien'", Text(predicate));
    }

    [Fact]
    public void Equality_Bounds_Are_Written_As_Equality()
    {
        var bounds = SeekBounds.Equality(TestKey.Of([42], "Id"));

        Assert.Equal("Id = 42", Text(bounds));
    }

    [Fact]
    public void Half_Open_Range_Is_Written_With_Both_Sides()
    {
        var bounds = SeekBounds.Between(TestKey.Of([10], "Id"), TestKey.Of([20], "Id"), true, false);

        Assert.Equal("Id >= 10 AND Id < 20", Text(bounds));
    }

    [Fact]
    public void Composite_Bounds_Are_Written_As_A_Row_Comparison()
    {
        var bounds = SeekBounds.Equality(TestKey.Of([1, 2], "CustomerId", "OrderId"));

        Assert.Equal("(CustomerId, OrderId) = (1, 2)", Text(bounds));
    }

    [Fact]
    public void Unnamed_Key_Columns_Fall_Back_To_Positions()
    {
        var bounds = SeekBounds.Equality(TestKey.Of(7));

        Assert.Equal("Key1 = 7", Text(bounds));
    }

    [Fact]
    public void Unbounded_Range_Is_Written_As_All()
    {
        Assert.Equal("ALL", Text(SeekBounds.All));
    }

    private static AccessExpression Column(int ordinal, string name)
    {
        return new AccessExpression.Column(ordinal, name);
    }

    private static AccessExpression Constant(long value)
    {
        return new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, value));
    }

    private static string Text(AccessPredicate predicate)
    {
        return PredicateWriter.ToText(PredicateWriter.Write(predicate));
    }

    private static string Text(SeekBounds bounds)
    {
        return PredicateWriter.ToText(PredicateWriter.Write(bounds));
    }
}
