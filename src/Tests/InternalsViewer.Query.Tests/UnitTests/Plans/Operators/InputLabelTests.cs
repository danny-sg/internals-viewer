using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Operators;

[Trait("Category", "Unit")]
[Trait("Area", "Plans")]
public class InputLabelTests
{
    [Fact]
    public void Hash_Join_Inputs_Are_The_Build_And_The_Probe()
    {
        var join = Parent("Hash Match", "Inner Join");

        Assert.Equal([InputLabel.Build, InputLabel.Probe], join.Children.Select(c => InputLabel.Get(join, c)));
    }

    [Fact]
    public void Nested_Loops_Inputs_Are_The_Outer_Loop_And_The_Inner_Loop()
    {
        var loops = Parent("Nested Loops", "Inner Join");

        Assert.Equal([InputLabel.OuterLoop, InputLabel.InnerLoop], loops.Children.Select(c => InputLabel.Get(loops, c)));
    }

    [Fact]
    public void Merge_Join_Inputs_Are_The_Outer_And_The_Inner()
    {
        var merge = Parent("Merge Join", "Inner Join");

        Assert.Equal([InputLabel.Outer, InputLabel.Inner], merge.Children.Select(c => InputLabel.Get(merge, c)));
    }

    [Fact]
    public void A_Hash_Aggregate_Input_Has_No_Label()
    {
        var child = new PlanNode { NodeId = 2, PhysicalOperator = "Table Scan" };

        var aggregate = new PlanNode { NodeId = 1, PhysicalOperator = "Hash Match", LogicalOperator = "Aggregate", Children = [child] };

        Assert.Null(InputLabel.Get(aggregate, child));
    }

    private static PlanNode Parent(string physicalOperator, string logicalOperator)
        => new()
        {
            NodeId = 1,
            PhysicalOperator = physicalOperator,
            LogicalOperator = logicalOperator,
            Children =
            [
                new PlanNode { NodeId = 2, PhysicalOperator = "Clustered Index Scan" },
                new PlanNode { NodeId = 3, PhysicalOperator = "Index Seek" }
            ]
        };
}
