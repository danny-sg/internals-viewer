using System.Data;
using System.Text;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;

namespace InternalsViewer.Execution.Tests.UnitTests.AccessPaths;

public class IntrinsicFunctionsTests
{
    private static readonly IRowValueSource Row = new TestRowValueSource(10);

    [Fact]
    public void Upper_Cases_Text()
    {
        Assert.Equal("HELLO", GetString(Resolve(Function("UPPER", Text("hello")))));
    }

    [Fact]
    public void Len_Ignores_Trailing_Spaces()
    {
        Assert.Equal(3, Resolve(Function("LEN", Text("abc   "))).Numeric);
    }

    [Fact]
    public void Round_Rounds_Half_Away_From_Zero()
    {
        var value = new AccessExpression.Constant(AccessValue.FromDecimal(SqlDbType.Decimal, 2.5m));

        Assert.Equal(3m, Resolve(Function("ROUND", value, Integer(0))).ToDecimal());
    }

    [Fact]
    public void Round_Takes_A_Negative_Length()
    {
        Assert.Equal(1300, Resolve(Function("ROUND", Integer(1250), Integer(-2))).Numeric);
    }

    [Fact]
    public void Substring_Is_One_Based()
    {
        Assert.Equal("ell", GetString(Resolve(Function("SUBSTRING", Text("hello"), Integer(2), Integer(3)))));
    }

    [Fact]
    public void Substring_Counts_A_Start_Before_The_Text_Against_The_Length()
    {
        Assert.Equal("he", GetString(Resolve(Function("SUBSTRING", Text("hello"), Integer(0), Integer(3)))));
    }

    [Fact]
    public void Charindex_Matches_Case_Insensitively()
    {
        Assert.Equal(3, Resolve(Function("CHARINDEX", Text("LL"), Text("hello"))).Numeric);
    }

    [Fact]
    public void Charindex_Returns_Zero_When_Absent()
    {
        Assert.Equal(0, Resolve(Function("CHARINDEX", Text("xyz"), Text("hello"))).Numeric);
    }

    [Fact]
    public void Left_And_Right_Take_The_Ends()
    {
        Assert.Equal("he", GetString(Resolve(Function("LEFT", Text("hello"), Integer(2)))));
        Assert.Equal("lo", GetString(Resolve(Function("RIGHT", Text("hello"), Integer(2)))));
    }

    [Fact]
    public void Replace_Matches_Case_Insensitively()
    {
        Assert.Equal("heLLo", GetString(Resolve(Function("REPLACE", Text("hello"), Text("LL"), Text("LL")))));
    }

    [Fact]
    public void IsNull_Substitutes_The_Fallback()
    {
        var value = new AccessExpression.Constant(AccessValue.FromNull(SqlDbType.Int));

        Assert.Equal(5, Resolve(Function("ISNULL", value, Integer(5))).Numeric);
    }

    [Fact]
    public void Null_Propagates_Through_String_Functions()
    {
        var value = new AccessExpression.Constant(AccessValue.FromNull(SqlDbType.NVarChar));

        Assert.True(Resolve(Function("UPPER", value)).IsNull);
    }

    [Fact]
    public void Getdate_Returns_The_Query_Time()
    {
        var queryTime = new DateTime(2026, 7, 30, 12, 0, 0);

        var result = Resolve(Function("GETDATE"), new EvaluationContext(queryTime));

        Assert.Equal(queryTime.Ticks, result.Numeric);
        Assert.Equal(SqlDbType.DateTime, result.DataType);
    }

    [Fact]
    public void Power_Of_A_Column_Compares_Against_A_Constant()
    {
        var predicate = new AccessPredicate.Comparison(Function("POWER", new AccessExpression.Column(0, "Id"), Integer(2)),
                                                       ComparisonOperator.Equal,
                                                       Integer(100));

        Assert.True(PredicateEvaluator.Evaluate(predicate, Row));
    }

    [Fact]
    public void Conditional_Takes_Then_When_True()
    {
        var condition = new AccessPredicate.Comparison(Integer(1), ComparisonOperator.Equal, Integer(1));

        var conditional = new AccessExpression.Conditional(condition, Integer(10), Integer(20));

        Assert.Equal(10, Resolve(conditional).Numeric);
    }

    [Fact]
    public void Conditional_Takes_Else_When_Unknown()
    {
        var condition = new AccessPredicate.Comparison(new AccessExpression.Constant(AccessValue.FromNull(SqlDbType.Int)),
                                                       ComparisonOperator.Equal,
                                                       Integer(1));

        var conditional = new AccessExpression.Conditional(condition, Integer(10), Integer(20));

        Assert.Equal(20, Resolve(conditional).Numeric);
    }

    [Fact]
    public void Add_Concatenates_Strings()
    {
        var expression = new AccessExpression.Arithmetic(ArithmeticOperator.Add, Text("foo"), Text("bar"));

        Assert.Equal("foobar", GetString(Resolve(expression)));
    }

    [Fact]
    public void Add_With_A_Null_String_Is_Null()
    {
        var expression = new AccessExpression.Arithmetic(ArithmeticOperator.Add,
                                                         Text("foo"),
                                                         new AccessExpression.Constant(AccessValue.FromNull(SqlDbType.NVarChar)));

        Assert.True(Resolve(expression).IsNull);
    }

    [Fact]
    public void Concat_Treats_Null_As_Empty()
    {
        var value = new AccessExpression.Constant(AccessValue.FromNull(SqlDbType.NVarChar));

        Assert.Equal("ab", GetString(Resolve(Function("CONCAT", Text("a"), value, Text("b")))));
    }

    [Fact]
    public void Concat_Converts_Numbers_To_Text()
    {
        Assert.Equal("a1", GetString(Resolve(Function("CONCAT", Text("a"), Integer(1)))));
    }

    [Fact]
    public void ConcatWs_Separates_And_Skips_Nulls()
    {
        var value = new AccessExpression.Constant(AccessValue.FromNull(SqlDbType.NVarChar));

        Assert.Equal("a-b", GetString(Resolve(Function("CONCAT_WS", Text("-"), Text("a"), value, Text("b")))));
    }

    [Fact]
    public void ConcatWs_With_A_Null_Separator_Joins_Without_One()
    {
        var separator = new AccessExpression.Constant(AccessValue.FromNull(SqlDbType.NVarChar));

        Assert.Equal("ab", GetString(Resolve(Function("CONCAT_WS", separator, Text("a"), Text("b")))));
    }

    [Fact]
    public void Fixed_Length_Text_Ignores_Trailing_Padding()
    {
        var stored = new AccessExpression.Constant(AccessValueFactory.FromText(SqlDbType.Char, "Row 5000        "));

        var literal = new AccessExpression.Constant(AccessValueFactory.FromText(SqlDbType.VarChar, "Row 5000"));

        var predicate = new AccessPredicate.Comparison(stored, ComparisonOperator.Equal, literal);

        Assert.True(PredicateEvaluator.Evaluate(predicate, Row));
    }

    [Fact]
    public void Narrow_Row_Text_Compares_Against_A_Wide_Literal()
    {
        var stored = new AccessExpression.Constant(AccessValueFactory.FromText(SqlDbType.VarChar, "Hello"));

        var literal = new AccessExpression.Constant(AccessValueFactory.FromText(SqlDbType.NVarChar, "hello"));

        var predicate = new AccessPredicate.Comparison(stored, ComparisonOperator.Equal, literal);

        Assert.True(PredicateEvaluator.Evaluate(predicate, Row));
    }

    private static AccessValue Resolve(AccessExpression expression, EvaluationContext? context = null)
    {
        return PredicateEvaluator.Resolve(expression, Row, context);
    }

    private static AccessExpression.Function Function(string name, params AccessExpression[] arguments)
    {
        return new AccessExpression.Function(name, [.. arguments]);
    }

    private static AccessExpression Integer(long value)
    {
        return new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, value));
    }

    private static AccessExpression Text(string value)
    {
        return new AccessExpression.Constant(AccessValueFactory.FromText(SqlDbType.NVarChar, value));
    }

    private static string GetString(AccessValue value)
    {
        return Encoding.Unicode.GetString(value.Data.Span);
    }
}
