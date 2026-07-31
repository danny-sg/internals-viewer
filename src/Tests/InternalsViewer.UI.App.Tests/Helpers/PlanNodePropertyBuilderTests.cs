using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Tests.Helpers;

public class PlanNodePropertyBuilderTests
{
    [Theory]
    [InlineData("Index Scan")]
    [InlineData("Clustered Index Scan")]
    [InlineData("Index Seek")]
    [InlineData("Key Lookup")]
    [InlineData("RID Lookup")]
    public void Scan_Mode_Is_Shown_For_An_Operator_That_Reads(string physicalOperator)
    {
        var properties = Build(physicalOperator);

        Assert.Contains(Storage(properties), p => p.Name == "Scan Mode");
    }

    [Theory]
    [InlineData("Filter")]
    [InlineData("Compute Scalar")]
    [InlineData("Nested Loops")]
    [InlineData("Sort")]
    [InlineData("Table Insert")]
    public void Scan_Mode_Is_Hidden_For_An_Operator_That_Does_Not_Read(string physicalOperator)
    {
        var properties = Build(physicalOperator);

        Assert.DoesNotContain(Storage(properties), p => p.Name == "Scan Mode");
    }

    [Fact]
    public void The_Rest_Of_The_Storage_Group_Survives_When_Scan_Mode_Is_Hidden()
    {
        var storage = Storage(Build("Filter"));

        Assert.Contains(storage, p => p.Name == "Object");
        Assert.Contains(storage, p => p.Name == "Direction");
    }

    private static List<PlanNodeProperty> Build(string physicalOperator)
    {
        var node = new PlanNode
        {
            PhysicalOperator = physicalOperator,
            Table = "ClusteredTable",
            ScanInfo = new ScanInfo()
        };

        return PlanNodePropertyBuilder.Build(node, scanMode: new ScanModeResult(ScanMode.LeafChain, "test"));
    }

    private static List<PlanNodeProperty> Storage(List<PlanNodeProperty> properties)
        => properties.Single(p => p.Name == "Storage").Children;
}
