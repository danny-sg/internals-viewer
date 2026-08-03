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

    [Fact]
    public void Operator_Memory_Grants_Are_Shown_In_Kilobytes()
    {
        var node = new PlanNode
        {
            PhysicalOperator = "Sort",
            MemoryGrant = new PlanMemoryGrant { InputKb = 1280, OutputKb = 1152, UsedKb = 352 }
        };

        var memory = Group(PlanNodePropertyBuilder.Build(node), "Memory Grant");

        Assert.Equal("1,280 KB", memory.Single(p => p.Name == "Input Memory Grant").Value);
        Assert.Equal("1,152 KB", memory.Single(p => p.Name == "Output Memory Grant").Value);
        Assert.Equal("352 KB", memory.Single(p => p.Name == "Used Memory Grant").Value);
    }

    [Fact]
    public void An_Absent_Operator_Memory_Grant_Value_Is_Omitted()
    {
        var node = new PlanNode
        {
            PhysicalOperator = "Hash Match",
            MemoryGrant = new PlanMemoryGrant { UsedKb = 64 }
        };

        var memory = Group(PlanNodePropertyBuilder.Build(node), "Memory Grant");

        Assert.DoesNotContain(memory, p => p.Name == "Input Memory Grant");
        Assert.DoesNotContain(memory, p => p.Name == "Output Memory Grant");
        Assert.Single(memory);
    }

    [Fact]
    public void Query_Memory_Grant_Is_Shown_On_The_Statement_Node()
    {
        var node = new PlanNode
        {
            PhysicalOperator = "SELECT",
            IsStatement = true,
            QueryMemoryGrant = new QueryMemoryGrant
            {
                RequestedKb = 1920,
                GrantedKb = 1920,
                MaxUsedKb = 704,
                GrantWaitTimeSeconds = 3
            }
        };

        var memory = Group(PlanNodePropertyBuilder.Build(node), "Memory Grant");

        Assert.Equal("1,920 KB", memory.Single(p => p.Name == "Requested Memory").Value);
        Assert.Equal("1,920 KB", memory.Single(p => p.Name == "Granted Memory").Value);
        Assert.Equal("704 KB", memory.Single(p => p.Name == "Max Used Memory").Value);
        Assert.True(memory.Single(p => p.Name == "Grant Wait Time").IsValueHighlighted);
    }

    [Fact]
    public void A_Node_Without_Memory_Information_Has_No_Memory_Grant_Group()
    {
        var properties = Build("Sort");

        Assert.DoesNotContain(properties, p => p.Name == "Memory Grant");
    }

    private static List<PlanNodeProperty> Group(List<PlanNodeProperty> properties, string name)
        => properties.Single(p => p.Name == name).Children;

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
