using InternalsViewer.Query.Plans.Joins;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Joins;

public class HashJoinResolverTests
{
    [Fact]
    public void Resolve_Returns_The_Sides_For_A_Hash_Match_Of_Two_Reads()
    {
        var build = Scan("TableA");

        var probe = Scan("TableB");

        var join = Join(build, probe);

        var result = HashJoinResolver.Resolve(join);

        Assert.NotNull(result);
        Assert.Same(join, result.Join);
        Assert.Same(build, result.Build);
        Assert.Same(probe, result.Probe);
    }

    [Fact]
    public void Resolve_Returns_Null_For_A_Non_Hash_Operator()
    {
        var result = HashJoinResolver.Resolve(Scan("TableA"));

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_For_A_Hash_Aggregate()
    {
        var aggregate = new PlanNode
        {
            NodeId = 1,
            PhysicalOperator = "Hash Match",
            LogicalOperator = "Aggregate",
            Children = [Scan("TableA")],
            HashInfo = new HashInfo
            {
                BuildKeys = [new ColumnReference { Table = "[TableA]", Column = "Id" }],
                ProbeKeys = [new ColumnReference { Table = "[TableA]", Column = "Id" }]
            }
        };

        var result = HashJoinResolver.Resolve(aggregate);

        Assert.Null(result);
    }

    /// <summary>
    /// A side that is itself an operator resolves, because the trace builds that subtree too
    /// </summary>
    /// <remarks>
    /// The side names no table of its own, so the keys are matched against the tables read beneath it. Whether the operator can actually
    /// be simulated is settled by whoever builds the definition, not here.
    /// </remarks>
    [Fact]
    public void Resolve_Accepts_A_Side_That_Is_Itself_An_Operator()
    {
        var read = Scan("TableA");

        var sort = new PlanNode
        {
            NodeId = 2,
            PhysicalOperator = "Sort",
            Children = [read]
        };

        var result = HashJoinResolver.Resolve(Join(sort, Scan("TableB"), buildTable: "TableA"));

        Assert.NotNull(result);
        Assert.Same(sort, result!.Build);
    }

    [Fact]
    public void Resolve_Returns_Null_When_A_Key_Names_A_Table_The_Side_Never_Reads()
    {
        var result = HashJoinResolver.Resolve(Join(Scan("TableA"), Scan("TableB"), buildTable: "TableZ"));

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_Without_Hash_Keys()
    {
        var join = Join(Scan("TableA"), Scan("TableB"));

        join.HashInfo = null;

        var result = HashJoinResolver.Resolve(join);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_When_Key_Counts_Differ()
    {
        var join = Join(Scan("TableA"), Scan("TableB"));

        join.HashInfo = new HashInfo
        {
            BuildKeys = [new ColumnReference { Table = "[TableA]", Column = "Id" }],
            ProbeKeys = []
        };

        var result = HashJoinResolver.Resolve(join);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_When_Key_Tables_Do_Not_Match_The_Sides()
    {
        var join = Join(Scan("TableA"), Scan("TableB"));

        join.HashInfo = new HashInfo
        {
            BuildKeys = [new ColumnReference { Table = "[SomeOtherTable]", Column = "Id" }],
            ProbeKeys = [new ColumnReference { Table = "[TableB]", Column = "Id" }]
        };

        var result = HashJoinResolver.Resolve(join);

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

    private static PlanNode Join(PlanNode build, PlanNode probe, string? buildTable = null)
    {
        return new PlanNode
        {
            NodeId = 1,
            PhysicalOperator = "Hash Match",
            LogicalOperator = "Inner Join",
            Children = [build, probe],
            HashInfo = new HashInfo
            {
                BuildKeys = [new ColumnReference { Table = $"[{buildTable ?? build.Table}]", Column = "Id" }],
                ProbeKeys = [new ColumnReference { Table = $"[{probe.Table}]", Column = "Id" }]
            }
        };
    }
}
