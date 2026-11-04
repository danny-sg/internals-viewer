using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class OffsetProviderTests
{
    [Fact]
    public void SQL_Server_2022_Uses_The_SQL_Server_2022_Plan_Node_Layout()
    {
        Assert.Same(PlanNodeLayout.SqlServer2022, new OffsetProvider(new Version(16, 0, 1210, 3)).PlanNode);
    }

    [Fact]
    public void SQL_Server_2025_Uses_The_SQL_Server_2025_Plan_Node_Layout()
    {
        Assert.Same(PlanNodeLayout.SqlServer2025, new OffsetProvider(new Version(17, 0, 5015, 4)).PlanNode);
    }

    [Fact]
    public void An_Unknown_Version_Uses_The_Latest_Plan_Node_Layout()
    {
        Assert.Same(PlanNodeLayout.SqlServer2025, new OffsetProvider(null).PlanNode);
    }
}
