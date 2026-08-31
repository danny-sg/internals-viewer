using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Plan;

namespace InternalsViewer.UI.App.Tests.Controls.Plan;

[Trait("Category", "Unit")]
public class PlanIconResolverTests
{
    [Fact]
    public void Resolves_A_Statement_To_Its_Statement_Icon()
    {
        var node = new PlanNode { IsStatement = true, PhysicalOperator = "SELECT" };

        Assert.Equal("ms-appx:///Controls/Plan/Icons/Select.svg", PlanIconResolver.Resolve(node).ToString());
    }

    [Fact]
    public void Falls_Back_To_The_Generic_Statement_Icon_For_An_Unknown_Statement()
    {
        var node = new PlanNode { IsStatement = true, PhysicalOperator = "COND" };

        Assert.Equal("ms-appx:///Controls/Plan/Icons/Statement.svg", PlanIconResolver.Resolve(node).ToString());
    }

    [Fact]
    public void Compacts_A_Physical_Operator_Name_To_Its_Icon()
    {
        var node = new PlanNode { PhysicalOperator = "Clustered Index Seek" };

        Assert.Equal("ms-appx:///Controls/Plan/Icons/ClusteredIndexSeek.svg", PlanIconResolver.Resolve(node).ToString());
    }

    [Fact]
    public void Prefers_The_Logical_Operator_Override_For_A_Key_Lookup()
    {
        var node = new PlanNode { PhysicalOperator = "Clustered Index Seek", LogicalOperator = "Key Lookup" };

        Assert.Equal("ms-appx:///Controls/Plan/Icons/KeyLookup.svg", PlanIconResolver.Resolve(node).ToString());
    }

    [Fact]
    public void Falls_Back_To_The_Default_Icon_For_An_Unknown_Operator()
    {
        var node = new PlanNode { PhysicalOperator = "Adaptive Join" };

        Assert.Equal("ms-appx:///Controls/Plan/Icons/Default.svg", PlanIconResolver.Resolve(node).ToString());
    }
}
