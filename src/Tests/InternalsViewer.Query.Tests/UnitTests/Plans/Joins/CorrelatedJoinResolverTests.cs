
using InternalsViewer.Query.Plans.Joins;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Joins;

public class CorrelatedJoinResolverTests
{
    [Fact]
    public void Resolve_Returns_The_Sides_For_A_Correlated_Inner_Seek()
    {
        var outer = Seek();

        var inner = Lookup();

        var join = Join(outer, inner);

        var result = CorrelatedJoinResolver.Resolve(join);

        Assert.NotNull(result);
        Assert.Same(join, result.Join);
        Assert.Same(outer, result.Outer);
        Assert.Same(inner, result.Inner);
    }

    [Fact]
    public void Resolve_Returns_Null_When_The_Outer_Does_Not_Output_The_Correlated_Column()
    {
        var outer = Seek();

        outer.OutputColumns = [new ColumnReference { Table = "[ClusteredTable]", Column = "OtherColumn" }];

        var result = CorrelatedJoinResolver.Resolve(Join(outer, Lookup()));

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_When_The_Inner_Is_Not_Correlated()
    {
        var inner = Lookup();

        inner.PredicateInfo = new PredicateInfo();

        var result = CorrelatedJoinResolver.Resolve(Join(Seek(), inner));

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_For_A_Non_Join_Operator()
    {
        var result = CorrelatedJoinResolver.Resolve(Seek());

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_Returns_Null_When_The_Inner_Is_Not_A_Read()
    {
        var inner = Lookup();

        inner.PhysicalOperator = "Compute Scalar";

        var result = CorrelatedJoinResolver.Resolve(Join(Seek(), inner));

        Assert.Null(result);
    }

    [Fact]
    public void ResolveFromInner_Finds_The_Owning_Join()
    {
        var inner = Lookup();

        var join = Join(Seek(), inner);

        var root = new PlanNode
        {
            NodeId = 0,
            PhysicalOperator = "SELECT",
            Children = [join]
        };

        var result = CorrelatedJoinResolver.ResolveFromInner(root, inner);

        Assert.NotNull(result);
        Assert.Same(join, result.Join);
        Assert.Same(inner, result.Inner);
    }

    [Fact]
    public void ResolveFromInner_Returns_Null_When_The_Parent_Is_Not_A_Correlated_Join()
    {
        var inner = Lookup();

        inner.PredicateInfo = new PredicateInfo();

        var join = Join(Seek(), inner);

        var result = CorrelatedJoinResolver.ResolveFromInner(join, inner);

        Assert.Null(result);
    }

    [Fact]
    public void Outer_Reference_Is_Composed_From_Table_And_Column()
    {
        var column = new CorrelatedSeekColumn("Id", "ClusteredTable", "Id");

        Assert.Equal("ClusteredTable.Id", column.OuterReference);
    }

    private static PlanNode Lookup()
    {
        return new PlanNode
        {
            NodeId = 5,
            PhysicalOperator = "Key Lookup",
            Table = "ClusteredTable",
            ScanInfo = new ScanInfo { IsLookup = true },
            PredicateInfo = new PredicateInfo
            {
                CorrelatedSeekColumns = [new CorrelatedSeekColumn("Id", "ClusteredTable", "Id")]
            }
        };
    }

    private static PlanNode Seek()
    {
        return new PlanNode
        {
            NodeId = 3,
            PhysicalOperator = "Index Seek",
            Table = "ClusteredTable",
            OutputColumns = [new ColumnReference { Table = "[ClusteredTable]", Column = "Id" }]
        };
    }

    private static PlanNode Join(PlanNode outer, PlanNode inner)
    {
        return new PlanNode
        {
            NodeId = 1,
            PhysicalOperator = "Nested Loops",
            Children = [outer, inner]
        };
    }
}
