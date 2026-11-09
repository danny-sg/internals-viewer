using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Operators;

[Trait("Category", "Unit")]
public class ExpressionPullResolverTests
{
    private const string Value = "Expr1";

    private const string Other = "Expr2";

    [Fact]
    public void A_Value_Nothing_Uses_Is_Pulled_When_The_Row_Is_Sent()
    {
        var statement = Statement(ComputeScalar(Value));

        Assert.Equal([(statement, ExpressionPullSite.FirstInput)], Resolve(statement));
    }

    [Fact]
    public void An_Operator_That_Uses_The_Value_Pulls_It_And_The_Row_Send_Pulls_It_Again()
    {
        var filter = Operator("Filter", [Value], [Value], ComputeScalar(Value));

        var statement = Statement(filter);

        Assert.Equal([(filter, ExpressionPullSite.FirstInput), (statement, ExpressionPullSite.FirstInput)], Resolve(statement));
    }

    [Fact]
    public void An_Operator_That_Only_Passes_The_Value_Along_Does_Not_Pull_It()
    {
        var filter = Operator("Filter", [Other], [Value], ComputeScalar(Value));

        var statement = Statement(filter);

        Assert.Equal([(statement, ExpressionPullSite.FirstInput)], Resolve(statement));
    }

    [Fact]
    public void A_Sort_Pulls_Every_Value_It_Stores_And_Nothing_Above_Pulls_Them_Again()
    {
        var sort = Operator("Sort", [Value], [Value, Other], ComputeScalar(Value, Other));

        var pull = Assert.Single(ExpressionPullResolver.Resolve(Plan(Statement(sort))));

        Assert.Equal((sort, 2), (pull.Puller, pull.Expressions.Count));
    }

    [Fact]
    public void A_Hash_Build_Input_Is_Stored_So_Nothing_Above_Pulls_It_Again()
    {
        var hash = Operator("Hash Match", [Value], [Value], ComputeScalar(Value), Scan());

        var statement = Statement(hash);

        Assert.Equal([(hash, ExpressionPullSite.FirstInput)], Resolve(statement));
    }

    [Fact]
    public void A_Hash_Probe_Input_Is_The_Second_Input_And_Is_Not_Stored()
    {
        var hash = Operator("Hash Match", [Value], [Value], Scan(), ComputeScalar(Value));

        var statement = Statement(hash);

        Assert.Equal([(hash, ExpressionPullSite.SecondInput), (statement, ExpressionPullSite.FirstInput)], Resolve(statement));
    }

    [Fact]
    public void An_Outer_Reference_Is_Pulled_By_The_Inner_Operator_That_Uses_It()
    {
        var seek = Operator("Index Seek", [Value], []);

        var loops = Operator("Nested Loops", [], [], ComputeScalar(Value), seek);

        loops.OuterReferences = [Value];

        Assert.Equal([(seek, ExpressionPullSite.FirstInput)], Resolve(Statement(loops)));
    }

    [Fact]
    public void A_Runtime_Constant_Is_Pulled_Once_At_Startup()
    {
        var computeScalar = ComputeScalar(Value);

        computeScalar.DefinedValues = [computeScalar.DefinedValues[0] with { References = ["ConstExpr3"] }];

        var statement = Statement(computeScalar);

        Assert.Equal([(statement, ExpressionPullSite.Startup)], Resolve(statement));
    }

    [Fact]
    public void A_Value_Built_On_Another_Compute_Scalar_Is_Pulled_With_It()
    {
        var lower = ComputeScalar(Value);

        var upper = ComputeScalar(Other);

        upper.DefinedValues = [upper.DefinedValues[0] with { References = [Value] }];

        upper.Children = [lower];

        var filter = Operator("Filter", [Other], [], upper);

        var pulls = ExpressionPullResolver.Resolve(Plan(Statement(filter)));

        Assert.Equal([lower, upper], pulls.Select(p => p.ComputeScalar).OrderBy(n => n.OutputColumns[0].Column));

        Assert.All(pulls, p => Assert.Same(filter, p.Puller));
    }

    [Fact]
    public void A_Batch_Compute_Scalar_Is_Left_To_The_Trace()
    {
        var computeScalar = ComputeScalar(Value);

        computeScalar.ExecutionMode = ExecutionMode.Batch;

        Assert.Empty(Resolve(Statement(computeScalar)));
    }

    [Fact]
    public void A_Value_Sent_With_The_Row_Is_Part_Of_The_Output()
    {
        var pull = Assert.Single(ExpressionPullResolver.Resolve(Plan(Statement(ComputeScalar(Value)))));

        Assert.Equal([Value], pull.OutputExpressions);
    }

    [Fact]
    public void A_Value_A_Filter_Only_Reads_Is_Not_Part_Of_Its_Output()
    {
        var filter = Operator("Filter", [Value], [Value], ComputeScalar(Value));

        var pulls = ExpressionPullResolver.Resolve(Plan(Statement(filter)));

        Assert.Empty(pulls.Single(p => ReferenceEquals(p.Puller, filter)).OutputExpressions);
    }

    [Fact]
    public void A_Sort_Key_That_Is_Not_Returned_Is_Not_Part_Of_The_Output()
    {
        var sort = Operator("Sort", [Value], [Other], ComputeScalar(Value, Other));

        var pull = Assert.Single(ExpressionPullResolver.Resolve(Plan(Statement(sort))));

        Assert.Equal(([Value, Other], [Other]), (pull.Expressions.ToArray(), pull.OutputExpressions.ToArray()), ArrayPairComparer);
    }

    [Fact]
    public void A_Join_Key_That_Is_Not_Returned_Is_Not_Part_Of_The_Output()
    {
        var hash = Operator("Hash Match", [Value], [], ComputeScalar(Value), Scan());

        var pull = Assert.Single(ExpressionPullResolver.Resolve(Plan(Statement(hash))));

        Assert.Empty(pull.OutputExpressions);
    }

    [Fact]
    public void A_Stored_Group_Key_Is_Part_Of_The_Output()
    {
        var hash = Operator("Hash Match", [Value], [Value], ComputeScalar(Value));

        var pull = Assert.Single(ExpressionPullResolver.Resolve(Plan(Statement(hash))));

        Assert.Equal([Value], pull.OutputExpressions);
    }

    private static EqualityComparer<(string[], string[])> ArrayPairComparer { get; }
        = EqualityComparer<(string[], string[])>.Create((x, y) => x.Item1.SequenceEqual(y.Item1) && x.Item2.SequenceEqual(y.Item2));

    private static List<(PlanNode Puller, ExpressionPullSite Site)> Resolve(PlanNode statement)
        => [.. ExpressionPullResolver.Resolve(Plan(statement)).Select(p => (p.Puller, p.Site))];

    private static ExecutionPlan Plan(PlanNode statement) => new(1) { Root = [statement] };

    private static PlanNode Statement(PlanNode child) => new() { IsStatement = true, PhysicalOperator = "SELECT", Children = [child] };

    private static PlanNode Scan() => new() { PhysicalOperator = "Index Scan" };

    private static PlanNode Operator(string name, string[] references, string[] output, params PlanNode[] children)
        => new() { PhysicalOperator = name, ReferencedValues = [.. references], OutputColumns = Columns(output), Children = [.. children] };

    private static PlanNode ComputeScalar(params string[] values)
        => new()
        {
            PhysicalOperator = "Compute Scalar",
            OutputColumns = Columns(values),
            DefinedValues = [.. values.Select(Defined)],
            Children = [Scan()]
        };

    private static DefinedValueInfo Defined(string value)
        => new() { Columns = [new ColumnReference { Column = value }], Expression = value };

    private static List<ColumnReference> Columns(string[] names) => [.. names.Select(n => new ColumnReference { Column = n })];
}
