using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.Query.Tests.UnitTests.Parsing.Plans;

public class MergeJoinResolverTests
{
    [Fact]
    public void Resolve_Returns_The_Sides_For_A_Merge_Of_Two_Reads()
    {
        var outer = Scan("TableA");

        var inner = Scan("TableB");

        var join = Join(outer, inner);

        var result = MergeJoinResolver.Resolve(join);

        Assert.NotNull(result);
        Assert.Same(join, result.Join);
        Assert.Same(outer, result.Outer);
        Assert.Same(inner, result.Inner);
    }

    [Fact]
    public void Resolve_Returns_Null_For_A_Non_Merge_Operator()
    {
        var result = MergeJoinResolver.Resolve(Scan("TableA"));

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_When_A_Side_Is_Not_A_Read()
    {
        var outer = Scan("TableA");

        outer.PhysicalOperator = "Sort";

        var result = MergeJoinResolver.Resolve(Join(outer, Scan("TableB")));

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_Without_Merge_Keys()
    {
        var join = Join(Scan("TableA"), Scan("TableB"));

        join.MergeInfo = null;

        var result = MergeJoinResolver.Resolve(join);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_When_Key_Tables_Do_Not_Match_The_Sides()
    {
        var join = Join(Scan("TableA"), Scan("TableB"));

        join.MergeInfo = new MergeInfo
        {
            OuterKeys = [new ColumnReference { Table = "[SomeOtherTable]", Column = "Id" }],
            InnerKeys = [new ColumnReference { Table = "[TableB]", Column = "Id" }]
        };

        var result = MergeJoinResolver.Resolve(join);

        Assert.Null(result);
    }

    private static PlanNode Scan(string table)
    {
        return new PlanNode
        {
            NodeId = 3,
            PhysicalOperator = "Clustered Index Scan",
            Table = table
        };
    }

    private static PlanNode Join(PlanNode outer, PlanNode inner)
    {
        return new PlanNode
        {
            NodeId = 1,
            PhysicalOperator = "Merge Join",
            Children = [outer, inner],
            MergeInfo = new MergeInfo
            {
                OuterKeys = [new ColumnReference { Table = $"[{outer.Table}]", Column = "Id" }],
                InnerKeys = [new ColumnReference { Table = $"[{inner.Table}]", Column = "Id" }]
            }
        };
    }
}
