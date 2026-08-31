using System.Xml.Linq;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Parsers.Predicates;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Parsers.Predicates;

[Trait("Category", "Unit")]
[Trait("Area", "Plans")]
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

    [Fact]
    public void Modulo_Comparison_Is_Parsed()
    {
        var arithmetic = $"""
                          <ScalarOperator><Arithmetic Operation="MOD">{Identifier("Id")}{Const("(2)")}</Arithmetic></ScalarOperator>
                          """;

        var comparison = Assert.IsType<AccessPredicate.Comparison>(Parse(Compare("EQ", arithmetic, Const("(0)"))));

        var expression = Assert.IsType<AccessExpression.Arithmetic>(comparison.Left);

        Assert.Equal(ArithmeticOperator.Modulo, expression.Operator);
        Assert.Equal("Id", Assert.IsType<AccessExpression.Column>(expression.Left).Name);
        Assert.Equal(2, Assert.IsType<AccessExpression.Constant>(expression.Right).Value.Numeric);
    }

    [Fact]
    public void Like_And_Modulo_Conjunction_Is_Parsed()
    {
        var like = $"""
                    <ScalarOperator>
                      <Intrinsic FunctionName="like">
                        {Identifier("TextField")}
                        {Const("'Clustered table row 10%'")}
                      </Intrinsic>
                    </ScalarOperator>
                    """;

        var arithmetic = $"""
                          <ScalarOperator><Arithmetic Operation="MOD">{Identifier("Id")}{Const("(2)")}</Arithmetic></ScalarOperator>
                          """;

        var xml = Logical("AND", like, Compare("EQ", arithmetic, Const("(0)")));

        var and = Assert.IsType<AccessPredicate.And>(Parse(xml));

        Assert.Equal(2, and.Predicates.Length);
        Assert.IsType<AccessPredicate.Like>(and.Predicates[0]);

        var comparison = Assert.IsType<AccessPredicate.Comparison>(and.Predicates[1]);

        Assert.IsType<AccessExpression.Arithmetic>(comparison.Left);
    }

    [Fact]
    public void Unknown_Arithmetic_Operation_Is_Not_Translated()
    {
        var arithmetic = $"""
                          <ScalarOperator><Arithmetic Operation="BIT_AND">{Identifier("Id")}{Const("(2)")}</Arithmetic></ScalarOperator>
                          """;

        Assert.Null(Parse(Compare("EQ", arithmetic, Const("(0)"))));
    }

    [Fact]
    public void Intrinsic_Function_Is_Parsed()
    {
        var upper = $"""<ScalarOperator><Intrinsic FunctionName="upper">{Identifier("Name")}</Intrinsic></ScalarOperator>""";

        var comparison = Assert.IsType<AccessPredicate.Comparison>(Parse(Compare("EQ", upper, Const("'A'"))));

        var function = Assert.IsType<AccessExpression.Function>(comparison.Left);

        Assert.Equal("UPPER", function.Name);
        Assert.Equal("Name", Assert.IsType<AccessExpression.Column>(function.Arguments[0]).Name);
    }

    [Fact]
    public void Getdate_Is_Parsed_Without_Arguments()
    {
        var getdate = """<ScalarOperator><Intrinsic FunctionName="getdate" /></ScalarOperator>""";

        var comparison = Assert.IsType<AccessPredicate.Comparison>(Parse(Compare("GT", Identifier("Expires"), getdate)));

        var function = Assert.IsType<AccessExpression.Function>(comparison.Right);

        Assert.Equal("GETDATE", function.Name);
        Assert.Empty(function.Arguments);
    }

    [Fact]
    public void Unknown_Intrinsic_Function_Is_Not_Translated()
    {
        var newid = """<ScalarOperator><Intrinsic FunctionName="newid" /></ScalarOperator>""";

        Assert.Null(Parse(Compare("EQ", Identifier("RowGuid"), newid)));
    }

    [Fact]
    public void If_Is_Parsed_As_A_Conditional()
    {
        var conditional = $"""
                           <ScalarOperator>
                             <IF>
                               <Condition>{Compare("EQ", Identifier("Status"), Const("(1)"))}</Condition>
                               <Then>{Const("(10)")}</Then>
                               <Else>{Const("(20)")}</Else>
                             </IF>
                           </ScalarOperator>
                           """;

        var comparison = Assert.IsType<AccessPredicate.Comparison>(Parse(Compare("EQ", conditional, Const("(10)"))));

        var expression = Assert.IsType<AccessExpression.Conditional>(comparison.Left);

        Assert.IsType<AccessPredicate.Comparison>(expression.Condition);
        Assert.Equal(10, Assert.IsType<AccessExpression.Constant>(expression.Then).Value.Numeric);
        Assert.Equal(20, Assert.IsType<AccessExpression.Constant>(expression.Else).Value.Numeric);
    }

    [Fact]
    public void If_With_An_Untranslatable_Branch_Is_Not_Translated()
    {
        var conditional = $"""
                           <ScalarOperator>
                             <IF>
                               <Condition>{Compare("BOGUS", Identifier("Status"), Const("(1)"))}</Condition>
                               <Then>{Const("(10)")}</Then>
                               <Else>{Const("(20)")}</Else>
                             </IF>
                           </ScalarOperator>
                           """;

        Assert.Null(Parse(Compare("EQ", conditional, Const("(10)"))));
    }

    [Fact]
    public void Aggregate_Is_Parsed_As_An_Expression()
    {
        var xml = $"""<ScalarOperator><Aggregate AggType="SUM" Distinct="false">{Identifier("Amount")}</Aggregate></ScalarOperator>""";

        var aggregate = Assert.IsType<AccessExpression.Aggregate>(new PredicateParser(_ => 0).ParseExpression(XElement.Parse(xml)));

        Assert.Equal("SUM", aggregate.Name);
        Assert.False(aggregate.IsDistinct);
        Assert.Equal("Amount", Assert.IsType<AccessExpression.Column>(aggregate.Arguments[0]).Name);
    }

    [Fact]
    public void Count_Star_Aggregate_Has_No_Arguments()
    {
        var xml = """<ScalarOperator><Aggregate AggType="countstar" Distinct="false" /></ScalarOperator>""";

        var aggregate = Assert.IsType<AccessExpression.Aggregate>(new PredicateParser(_ => 0).ParseExpression(XElement.Parse(xml)));

        Assert.Equal("COUNTSTAR", aggregate.Name);
        Assert.Empty(aggregate.Arguments);
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
