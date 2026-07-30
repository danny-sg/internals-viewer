using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.Query.Results;

namespace InternalsViewer.Query.Tests.UnitTests.Parsing.Plans;

public class ExpressionCatalogTests
{
    [Fact]
    public void Alias_Is_Assigned_From_The_Result_Set()
    {
        var plan = SumPlan();

        ExpressionCatalog.Populate([plan], [ResultSet("AmountTotal")]);

        Assert.Equal("AmountTotal", plan.Expressions!.Find("Expr1001")!.Alias);
    }

    [Fact]
    public void Display_Text_Prefers_The_Alias()
    {
        var plan = SumPlan();

        ExpressionCatalog.Populate([plan], [ResultSet("AmountTotal")]);

        Assert.Equal("AmountTotal", plan.Expressions!.GetDisplayText("[Expr1001]"));
    }

    [Fact]
    public void Alias_Is_Not_Assigned_When_Column_Counts_Differ()
    {
        var plan = SumPlan();

        ExpressionCatalog.Populate([plan], [ResultSet("AmountTotal", "Extra")]);

        Assert.Null(plan.Expressions!.Find("Expr1001")!.Alias);
    }

    [Fact]
    public void Expression_Text_Is_Expanded_Recursively()
    {
        var node = new PlanNode
        {
            NodeId = 2,
            DefinedValues =
            [
                Defined("Expr1001", "[Amount]+(1)"),
                Defined("Expr1002", "[Expr1001]*(2)")
            ]
        };

        var catalog = ExpressionCatalog.Build(new ExecutionPlan(0) { Root = [node] });

        Assert.Equal("[Amount]+(1)*(2)", catalog.GetExpandedText(catalog.Find("Expr1002")!));
    }

    [Fact]
    public void Self_Reference_Does_Not_Recurse()
    {
        var node = new PlanNode
        {
            NodeId = 2,
            DefinedValues = [Defined("Expr1001", "[Expr1001]+(1)")]
        };

        var catalog = ExpressionCatalog.Build(new ExecutionPlan(0) { Root = [node] });

        Assert.Equal("[Expr1001]+(1)", catalog.GetExpandedText(catalog.Find("Expr1001")!));
    }

    [Fact]
    public void Tokens_Are_Expanded_To_The_Alias()
    {
        var plan = SumPlan();

        ExpressionCatalog.Populate([plan], [ResultSet("AmountTotal")]);

        var expanded = plan.Expressions!.ExpandTokens([new PredicateToken(PredicateTokenType.Column, "Expr1001")]);

        var token = Assert.Single(expanded);

        Assert.Equal("AmountTotal", token.Text);
        Assert.Equal("Expr1001", token.Description);
    }

    [Fact]
    public void Parsed_Expression_Is_Rendered_As_Sql()
    {
        var aggregate = new AccessExpression.Aggregate("SUM", false, [new AccessExpression.Column(0, "Amount")]);

        var node = new PlanNode
        {
            NodeId = 2,
            DefinedValues = [Defined("Expr1001", "SUM([db].[dbo].[t].[Amount])", aggregate)]
        };

        var catalog = ExpressionCatalog.Build(new ExecutionPlan(0) { Root = [node] });

        Assert.Equal("SUM(Amount)", catalog.GetExpandedText(catalog.Find("Expr1001")!));
    }

    [Fact]
    public void Alias_Flows_Back_Through_An_Implicit_Convert()
    {
        var plan = ConvertPlan("CONVERT_IMPLICIT(numeric(38,6),[Expr1003],0)");

        ExpressionCatalog.Populate([plan], [ResultSet("AmountTotal")]);

        Assert.Equal("AmountTotal", plan.Expressions!.Find("Expr1002")!.Alias);
        Assert.Equal("AmountTotal", plan.Expressions!.Find("Expr1003")!.Alias);
    }

    [Fact]
    public void Mapping_Expansion_Keeps_The_Convert_And_Aliases_The_Reference()
    {
        var plan = ConvertPlan("CONVERT_IMPLICIT(numeric(38,6),[Expr1003],0)");

        ExpressionCatalog.Populate([plan], [ResultSet("AmountTotal")]);

        Assert.Equal("CONVERT_IMPLICIT(numeric(38,6),AmountTotal,0)",
                     plan.Expressions!.GetExpandedText(plan.Expressions!.Find("Expr1002")!));
    }

    [Fact]
    public void Unaliased_Mapping_Expansion_Inlines_The_Expression()
    {
        var plan = ConvertPlan("CONVERT_IMPLICIT(numeric(38,6),[Expr1003],0)");

        var catalog = ExpressionCatalog.Build(plan);

        Assert.Equal("CONVERT_IMPLICIT(numeric(38,6),SUM([t].[Amount]),0)",
                     catalog.GetExpandedText(catalog.Find("Expr1002")!));
    }

    [Fact]
    public void Explicit_Convert_Is_Not_A_Mapping()
    {
        var plan = ConvertPlan("CONVERT(int,[Expr1003],0)");

        ExpressionCatalog.Populate([plan], [ResultSet("AmountTotal")]);

        Assert.Equal("AmountTotal", plan.Expressions!.Find("Expr1002")!.Alias);
        Assert.Null(plan.Expressions!.Find("Expr1003")!.Alias);
    }

    private static ExecutionPlan ConvertPlan(string convertExpression)
    {
        var aggregate = new PlanNode
        {
            NodeId = 3,
            DefinedValues = [Defined("Expr1003", "SUM([t].[Amount])")]
        };

        var computeScalar = new PlanNode
        {
            NodeId = 2,
            OutputColumns = [new ColumnReference { Column = "Expr1002" }],
            DefinedValues = [Defined("Expr1002", convertExpression)],
            Children = [aggregate]
        };

        var statement = new PlanNode
        {
            NodeId = -1,
            IsStatement = true,
            Children = [computeScalar]
        };

        return new ExecutionPlan(0) { Root = [statement] };
    }

    private static ExecutionPlan SumPlan()
    {
        var aggregate = new PlanNode
        {
            NodeId = 2,
            DefinedValues = [Defined("Expr1001", "SUM([t].[Amount])")]
        };

        var root = new PlanNode
        {
            NodeId = 1,
            OutputColumns = [new ColumnReference { Column = "Expr1001" }],
            Children = [aggregate]
        };

        var statement = new PlanNode
        {
            NodeId = -1,
            IsStatement = true,
            Children = [root]
        };

        return new ExecutionPlan(0) { Root = [statement] };
    }

    private static DefinedValueInfo Defined(string name, string? expression, AccessExpression? parsed = null)
    {
        return new DefinedValueInfo
        {
            Columns = [new ColumnReference { Column = name }],
            Expression = expression,
            ParsedExpression = parsed
        };
    }

    private static QueryResultSet ResultSet(params string[] columns)
    {
        return new QueryResultSet
        {
            Columns = [.. columns.Select((name, index) => new ResultColumn(index, name, typeof(object), true))]
        };
    }
}
