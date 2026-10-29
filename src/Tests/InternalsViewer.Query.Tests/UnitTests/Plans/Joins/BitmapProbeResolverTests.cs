using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Joins;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Joins;

[Trait("Category", "Unit")]
[Trait("Area", "Plans")]
public class BitmapProbeResolverTests
{
    [Fact]
    public void Resolve_Links_The_Operator_That_Defines_The_Bitmap_To_The_Operator_That_Probes_It()
    {
        var build = Scan(2, "TableA");

        var probe = Scan(4, "TableB", probedBitmap: "Opt_Bitmap1004");

        var join = Join(build, probe, "Opt_Bitmap1004");

        var result = Assert.Single(BitmapProbeResolver.Resolve(Plan(join, build, probe)));

        Assert.Same(join, result.Creator);
        Assert.Same(probe, result.Probe);
        Assert.Equal("Opt_Bitmap1004", result.Bitmap);
    }

    [Fact]
    public void Resolve_Returns_Nothing_When_No_Operator_Defines_The_Probed_Bitmap()
    {
        var build = Scan(2, "TableA");

        var probe = Scan(4, "TableB", probedBitmap: "Opt_Bitmap1004");

        var join = Join(build, probe, "Opt_Bitmap1005");

        var result = BitmapProbeResolver.Resolve(Plan(join, build, probe));

        Assert.Empty(result);
    }

    [Fact]
    public void Resolve_Ignores_A_Table_Column_That_Shares_The_Bitmap_Name()
    {
        var build = new PlanNode
        {
            NodeId = 2,
            PhysicalOperator = "Clustered Index Scan",
            Table = "TableA",
            DefinedValues = [new DefinedValueInfo { Columns = [new ColumnReference { Table = "TableA", Column = "Opt_Bitmap1004" }] }]
        };

        var probe = Scan(4, "TableB", probedBitmap: "Opt_Bitmap1004");

        var result = BitmapProbeResolver.Resolve(Plan(build, probe));

        Assert.Empty(result);
    }

    private static ExecutionPlan Plan(params PlanNode[] nodes)
    {
        var plan = new ExecutionPlan(1);

        foreach (var node in nodes)
        {
            plan.NodesById[node.NodeId] = node;
        }

        return plan;
    }

    private static PlanNode Scan(int nodeId, string table, string? probedBitmap = null)
    {
        return new PlanNode
        {
            NodeId = nodeId,
            PhysicalOperator = "Clustered Index Scan",
            Table = table,
            ProbedBitmaps = probedBitmap is null ? [] : [probedBitmap]
        };
    }

    private static PlanNode Join(PlanNode build, PlanNode probe, string bitmap)
    {
        return new PlanNode
        {
            NodeId = 0,
            PhysicalOperator = "Hash Match",
            LogicalOperator = "Inner Join",
            Children = [build, probe],
            DefinedValues = [new DefinedValueInfo { Columns = [new ColumnReference { Column = bitmap }] }]
        };
    }
}
