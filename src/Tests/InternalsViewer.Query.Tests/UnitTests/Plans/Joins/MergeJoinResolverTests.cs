
using InternalsViewer.Query.Plans.Joins;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Joins;

[Trait("Category", "Unit")]
[Trait("Area", "Plans")]
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

    /// <summary>
    /// A side that is itself an operator resolves, because the trace builds that subtree too
    /// </summary>
    [Fact]
    public void Resolve_Accepts_A_Side_That_Is_Itself_An_Operator()
    {
        var read = Scan("TableA");

        var loops = new PlanNode
        {
            NodeId = 2,
            PhysicalOperator = "Nested Loops",
            Children = [read]
        };

        var result = MergeJoinResolver.Resolve(Join(loops, Scan("TableB"), outerTable: "TableA"));

        Assert.NotNull(result);
        Assert.Same(loops, result!.Outer);
    }

    [Fact]
    public void Resolve_Returns_Null_When_A_Key_Names_A_Table_The_Side_Never_Reads()
    {
        var result = MergeJoinResolver.Resolve(Join(Scan("TableA"), Scan("TableB"), outerTable: "TableZ"));

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

    private static PlanNode Join(PlanNode outer, PlanNode inner, string? outerTable = null)
    {
        return new PlanNode
        {
            NodeId = 1,
            PhysicalOperator = "Merge Join",
            Children = [outer, inner],
            MergeInfo = new MergeInfo
            {
                OuterKeys = [new ColumnReference { Table = $"[{outerTable ?? outer.Table}]", Column = "Id" }],
                InnerKeys = [new ColumnReference { Table = $"[{inner.Table}]", Column = "Id" }]
            }
        };
    }
}
