using System.Data;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Execution.Tests.UnitTests.AccessPaths.Text;

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
                                    .First(t => t.Type == PredicateTokenType.Column);

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

    [Fact]
    public void Probe_Is_Written_As_The_Values_Compared()
    {
        var probe = new AccessStep.Probe(0, 8, 4, -1)
        {
            Key = TestKey.Of(843),
            Target = TestKey.Of(1000),
            Width = 1,
            SearchRight = true
        };

        Assert.Equal("843 < 1000", Text(probe));
    }

    [Fact]
    public void Equal_Probe_Is_Written_With_An_Equality_Operator()
    {
        var probe = new AccessStep.Probe(0, 8, 4, 0)
        {
            Key = TestKey.Of(1000),
            Target = TestKey.Of(1000),
            Width = 1
        };

        Assert.Equal("1000 = 1000", Text(probe));
    }

    [Fact]
    public void Composite_Probe_Is_Written_As_A_Row_Comparison()
    {
        var probe = new AccessStep.Probe(0, 8, 4, 1)
        {
            Key = TestKey.Of(2, 5),
            Target = TestKey.Of(2, 3),
            Width = 2
        };

        Assert.Equal("(2, 5) > (2, 3)", Text(probe));
    }

    [Fact]
    public void Probe_Result_Is_Written_As_Its_Condition()
    {
        var probeResult = new AccessStep.ProbeResult(29)
        {
            Rule = SeekRule.LowestGreaterOrEqual,
            Target = TestKey.Of(5000),
            Width = 1
        };

        Assert.Equal(">= 5000", Text(probeResult));
    }

    [Fact]
    public void Node_Page_Probe_Result_Is_Written_With_The_Below_Target_Operator()
    {
        var probeResult = new AccessStep.ProbeResult(30)
        {
            Rule = SeekRule.HighestLess,
            Target = TestKey.Of(5000),
            Width = 1
        };

        Assert.Equal("< 5000", Text(probeResult));
    }

    [Fact]
    public void Probe_Result_Without_A_Rule_Writes_Nothing()
    {
        var probeResult = new AccessStep.ProbeResult(0);

        Assert.Equal(string.Empty, Text(probeResult));
    }

    [Fact]
    public void Probe_Only_Writes_The_Columns_Taking_Part_In_The_Comparison()
    {
        var probe = new AccessStep.Probe(0, 8, 4, -1)
        {
            Key = TestKey.Of(2, 5),
            Target = TestKey.Of(3),
            Width = 1,
            SearchRight = true
        };

        Assert.Equal("2 < 3", Text(probe));
    }

    [Fact]
    public void Function_Call_Is_Written()
    {
        var predicate = new AccessPredicate.Comparison(new AccessExpression.Function("UPPER", [Column(0, "Name")]),
                                                       ComparisonOperator.Equal,
                                                       Constant(1));

        Assert.Equal("UPPER(Name) = 1", Text(predicate));
    }

    [Fact]
    public void Function_Without_Arguments_Is_Written_With_Empty_Parentheses()
    {
        var predicate = new AccessPredicate.Comparison(Column(0, "Expires"),
                                                       ComparisonOperator.GreaterThan,
                                                       new AccessExpression.Function("GETDATE", []));

        Assert.Equal("Expires > GETDATE()", Text(predicate));
    }

    [Fact]
    public void Case_Expression_Is_Written_With_A_Chained_Else_As_A_Further_When()
    {
        var first = new AccessPredicate.Comparison(Column(0, "A"), ComparisonOperator.Equal, Constant(1));
        var second = new AccessPredicate.Comparison(Column(1, "B"), ComparisonOperator.Equal, Constant(2));

        var conditional = new AccessExpression.Conditional(first,
                                                           Constant(10),
                                                           new AccessExpression.Conditional(second, Constant(20), Constant(30)));

        var predicate = new AccessPredicate.Comparison(conditional, ComparisonOperator.Equal, Constant(10));

        Assert.Equal("CASE WHEN A = 1 THEN 10 WHEN B = 2 THEN 20 ELSE 30 END = 10", Text(predicate));
    }

    [Fact]
    public void Count_Star_Aggregate_Is_Written()
    {
        var aggregate = new AccessExpression.Aggregate("COUNTSTAR", false, []);

        Assert.Equal("COUNT(*)", PredicateWriter.ToText(PredicateWriter.Write(aggregate)));
    }

    [Fact]
    public void Distinct_Aggregate_Is_Written_With_Its_Argument()
    {
        var aggregate = new AccessExpression.Aggregate("SUM", true, [Column(0, "Amount")]);

        Assert.Equal("SUM(DISTINCT Amount)", PredicateWriter.ToText(PredicateWriter.Write(aggregate)));
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

    private static string Text(AccessStep.Probe probe)
    {
        return PredicateWriter.ToText(PredicateWriter.Write(probe));
    }

    private static string Text(AccessStep.ProbeResult probeResult)
    {
        return PredicateWriter.ToText(PredicateWriter.Write(probeResult));
    }
}
