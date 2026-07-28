using System.Xml.Linq;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;
using InternalsViewer.Query.Parsing.Plans.Predicates;

namespace InternalsViewer.Query.Tests.UnitTests.Parsing.Plans.Predicates;

public class PredicateParserTests
{
    [Fact]
    public void Comparison_Between_Column_And_Constant_Is_Parsed()
    {
        var xml = Compare("LT", Identifier("OrderId"), Const("100"));

        var predicate = Parse(xml);

        var comparison = Assert.IsType<AccessPredicate.Comparison>(predicate);

        Assert.Equal(ComparisonOperator.LessThan, comparison.Operator);

        var column = Assert.IsType<AccessExpression.Column>(comparison.Left);

        Assert.Equal("OrderId", column.Name);
        Assert.Equal(0, column.Ordinal);

        var constant = Assert.IsType<AccessExpression.Constant>(comparison.Right);

        Assert.Equal(100, constant.Value.Numeric);
    }

    [Theory]
    [InlineData("EQ", ComparisonOperator.Equal)]
    [InlineData("NE", ComparisonOperator.NotEqual)]
    [InlineData("LT", ComparisonOperator.LessThan)]
    [InlineData("LE", ComparisonOperator.LessThanOrEqual)]
    [InlineData("GT", ComparisonOperator.GreaterThan)]
    [InlineData("GE", ComparisonOperator.GreaterThanOrEqual)]
    [InlineData("IS", ComparisonOperator.Equal)]
    public void Compare_Operators_Map_To_Their_Equivalents(string compareOp, ComparisonOperator expected)
    {
        var predicate = Parse(Compare(compareOp, Identifier("OrderId"), Const("1")));

        Assert.Equal(expected, Assert.IsType<AccessPredicate.Comparison>(predicate).Operator);
    }

    [Fact]
    public void Unknown_Compare_Operator_Is_Not_Translated()
    {
        Assert.Null(Parse(Compare("BOGUS", Identifier("OrderId"), Const("1"))));
    }

    [Fact]
    public void And_Of_Two_Comparisons_Is_Parsed()
    {
        var xml = Logical("AND",
                          Compare("GE", Identifier("OrderId"), Const("10")),
                          Compare("LE", Identifier("OrderId"), Const("20")));

        var and = Assert.IsType<AccessPredicate.And>(Parse(xml));

        Assert.Equal(2, and.Predicates.Length);
    }

    [Fact]
    public void Or_Of_Two_Comparisons_Is_Parsed()
    {
        var xml = Logical("OR",
                          Compare("EQ", Identifier("OrderId"), Const("10")),
                          Compare("EQ", Identifier("OrderId"), Const("20")));

        Assert.Equal(2, Assert.IsType<AccessPredicate.Or>(Parse(xml)).Predicates.Length);
    }

    [Fact]
    public void Single_Operand_Conjunction_Is_Flattened()
    {
        var xml = Logical("AND", Compare("EQ", Identifier("OrderId"), Const("10")));

        Assert.IsType<AccessPredicate.Comparison>(Parse(xml));
    }

    [Fact]
    public void Not_Wraps_Its_Operand()
    {
        var xml = Logical("NOT", Compare("EQ", Identifier("OrderId"), Const("10")));

        var not = Assert.IsType<AccessPredicate.Not>(Parse(xml));

        Assert.IsType<AccessPredicate.Comparison>(not.Predicate);
    }

    [Fact]
    public void Is_Null_Takes_A_Scalar_Operand()
    {
        var xml = Logical("IS NULL", Identifier("OrderId"));

        var isNull = Assert.IsType<AccessPredicate.IsNull>(Parse(xml));

        Assert.Equal("OrderId", Assert.IsType<AccessExpression.Column>(isNull.Expression).Name);
    }

    [Fact]
    public void Is_Not_Null_Is_A_Negated_Null_Test()
    {
        var xml = Logical("IS NOT NULL", Identifier("OrderId"));

        var not = Assert.IsType<AccessPredicate.Not>(Parse(xml));

        Assert.IsType<AccessPredicate.IsNull>(not.Predicate);
    }

    [Fact]
    public void Nested_Conjunction_Preserves_Structure()
    {
        var xml = Logical("AND",
                          Compare("EQ", Identifier("OrderId"), Const("1")),
                          Logical("OR",
                                  Compare("EQ", Identifier("OrderId"), Const("2")),
                                  Compare("EQ", Identifier("OrderId"), Const("3"))));

        var and = Assert.IsType<AccessPredicate.And>(Parse(xml));

        Assert.IsType<AccessPredicate.Comparison>(and.Predicates[0]);
        Assert.Equal(2, Assert.IsType<AccessPredicate.Or>(and.Predicates[1]).Predicates.Length);
    }

    [Fact]
    public void Unresolved_Table_Column_Falls_Back_To_Its_Name()
    {
        var xml = Compare("EQ", Identifier("OrderId"), Const("1"));

        var parser = new PredicateParser(_ => null);

        var comparison = Assert.IsType<AccessPredicate.Comparison>(parser.Parse(XElement.Parse(xml)));

        var column = Assert.IsType<AccessExpression.Column>(comparison.Left);

        Assert.Equal(-1, column.Ordinal);
        Assert.Equal("OrderId", column.Name);
    }

    [Fact]
    public void Unresolvable_Expression_Column_Makes_The_Predicate_Untranslatable()
    {
        var expression = """
                         <ScalarOperator><Identifier><ColumnReference Column="Expr1002" /></Identifier></ScalarOperator>
                         """;

        var xml = Compare("EQ", expression, Const("1"));

        var parser = new PredicateParser(_ => null);

        Assert.Null(parser.Parse(XElement.Parse(xml)));
    }

    [Fact]
    public void A_Conjunction_Is_Dropped_When_Any_Operand_Is_Untranslatable()
    {
        var xml = Logical("AND",
                          Compare("EQ", Identifier("OrderId"), Const("1")),
                          Compare("BOGUS", Identifier("OrderId"), Const("2")));

        Assert.Null(Parse(xml));
    }

    [Fact]
    public void Predicate_Element_Is_Unwrapped()
    {
        var xml = $"<Predicate>{Compare("EQ", Identifier("OrderId"), Const("5"))}</Predicate>";

        var parser = new PredicateParser(_ => 0);

        Assert.IsType<AccessPredicate.Comparison>(
            parser.ParsePredicateElement(XElement.Parse(xml)));
    }

    [Fact]
    public void Like_Intrinsic_Is_Parsed()
    {
        var xml = $"""
                   <ScalarOperator>
                     <Intrinsic FunctionName="like">
                       <ScalarOperatorList>{Identifier("Name")}</ScalarOperatorList>
                       <ScalarOperatorList>{Const("'Sales%'")}</ScalarOperatorList>
                     </Intrinsic>
                   </ScalarOperator>
                   """;

        var like = Assert.IsType<AccessPredicate.Like>(Parse(xml));

        Assert.Equal("Sales%", like.Pattern);
    }

    private static AccessPredicate? Parse(string xml)
    {
        return new PredicateParser(_ => 0).Parse(XElement.Parse(xml));
    }

    private static string Compare(string op, string left, string right)
    {
        return $"""<ScalarOperator><Compare CompareOp="{op}">{left}{right}</Compare></ScalarOperator>""";
    }

    private static string Logical(string operation, params string[] operands)
    {
        return $"""
                <ScalarOperator><Logical Operation="{operation}">{string.Concat(operands)}</Logical></ScalarOperator>
                """;
    }

    private static string Identifier(string column)
    {
        return $"""
                <ScalarOperator><Identifier><ColumnReference Schema="[dbo]" Table="[Orders]"
                Column="{column}" /></Identifier></ScalarOperator>
                """;
    }

    private static string Const(string value)
    {
        return $"""<ScalarOperator><Const ConstValue="{System.Security.SecurityElement.Escape(value)}" /></ScalarOperator>""";
    }
}
