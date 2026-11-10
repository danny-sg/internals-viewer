using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.Tests.Helpers;

[Trait("Category", "Unit")]
public class PlanObjectNameFormatterTests
{
    [Theory]
    [InlineData("Clustered Index Seek")]
    [InlineData("Index Scan")]
    public void An_Index_Has_Its_Name_Above_The_Schema_And_Object(string physicalOperator)
    {
        var node = new PlanNode { PhysicalOperator = physicalOperator, Schema = "dbo", Table = "Orders", Index = "IX_Orders" };

        Assert.Equal("IX_Orders\ndbo.Orders", PlanObjectNameFormatter.Format(node));
    }

    [Fact]
    public void A_Heap_Has_Only_The_Schema_And_Object()
    {
        var node = new PlanNode { PhysicalOperator = "Table Scan", Schema = "dbo", Table = "Orders" };

        Assert.Equal("dbo.Orders", PlanObjectNameFormatter.Format(node));
    }

    [Fact]
    public void An_Object_With_No_Schema_Has_Only_Its_Name()
    {
        var node = new PlanNode { PhysicalOperator = "Table Scan", Table = "Orders" };

        Assert.Equal("Orders", PlanObjectNameFormatter.Format(node));
    }

    [Fact]
    public void An_Operator_That_Reads_No_Object_Has_No_Name()
    {
        var node = new PlanNode { PhysicalOperator = "Hash Match", LogicalOperator = "Inner Join" };

        Assert.Equal(string.Empty, PlanObjectNameFormatter.Format(node));
    }
}
