
using InternalsViewer.Query.Plans.Joins;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests.UnitTests.Plans.Joins;

[Trait("Category", "Unit")]
[Trait("Area", "Plans")]
public class CorrelatedJoinResolverTests
{
    [Fact]
    public void Outer_Reference_Is_Composed_From_Table_And_Column()
    {
        var column = new CorrelatedSeekColumn("Id", "ClusteredTable", "Id");

        Assert.Equal("ClusteredTable.Id", column.OuterReference);
    }

    [Fact]
    public void A_Rid_Lookup_Is_Recognised()
    {
        var inner = new PlanNode
        {
            NodeId = 5,
            PhysicalOperator = "RID Lookup",
            Table = "HeapTable",
            ScanInfo = new ScanInfo { IsLookup = true }
        };

        Assert.True(CorrelatedJoinResolver.IsRidLookup(inner));
    }

    [Fact]
    public void A_Key_Lookup_Is_Not_Taken_For_A_Rid_Lookup()
    {
        Assert.False(CorrelatedJoinResolver.IsRidLookup(Lookup()));
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
}
