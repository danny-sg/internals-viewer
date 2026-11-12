using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators.PlanNodes;

[Trait("Category", "Unit")]
public class PlanNodeFunctionLookupTests
{
    [Fact]
    public void Batch_Iterators_Are_Found_By_Their_Factory_And_The_Call_That_Hands_Over_A_Batch()
        => Assert.Equal([PlanNodeFunctionType.Iterator, PlanNodeFunctionType.BatchIterator, null, PlanNodeFunctionType.BatchFactory],
                        Types("CQScanHash::GetRow", "CBpQScanFilter::BpGetBatchData", "CBpQScanFilter::BpOpen", "CBpXteFilter::PbpqsGet"));

    [Fact]
    public void Every_Iterator_Method_Of_The_Batch_Helper_Is_A_Batch_Helper_Call()
        => Assert.Equal([PlanNodeFunctionType.BatchHelper, PlanNodeFunctionType.BatchHelper, PlanNodeFunctionType.Iterator],
                        Types("CQScanBatchHelper::GetRow", "CQScanBatchHelper::Open", "CQScanHash::GetRow"));

    [Fact]
    public void An_Open_Reached_Through_Its_Helper_Is_An_Iterator_Call()
        => Assert.Equal([PlanNodeFunctionType.Iterator, null], Types("CQScanSortNew::OpenHelper", "CQScanSortNew::BuildSortTable"));

    [Fact]
    public void Constructors_And_The_Query_Teardown_Are_Found_By_Name()
        => Assert.Equal([PlanNodeFunctionType.Constructor, PlanNodeFunctionType.Teardown, null],
                        Types("CQScanHash::CQScanHash", "CQueryScan::DestroyQuery", "CQueryScan::GetRow"));

    [Fact]
    public void Plan_Objects_Are_Known_By_Their_VTables()
    {
        var vtables = new Dictionary<ulong, VtableSymbol[]>
        {
            [0x7000] = [new("CXteHash", null)],
            [0x7100] = [new("CBpXteFilter", null)],
            [0x7200] = [new("CQScanHash", null)]
        };

        var planObjects = PlanNodeFunctionLookup.GetPlanObjects(vtables).OrderBy(p => p.Key).Select(p => (p.Key, p.Value));

        (ulong, PlanObjectClass)[] expected = [(0x7000, PlanObjectClass.Row), (0x7100, PlanObjectClass.Batch)];

        Assert.Equal(expected, planObjects);
    }

    [Fact]
    public void A_Shared_Vtable_Is_A_Plan_Object_When_Any_Of_Its_Classes_Is()
    {
        var vtables = new Dictionary<ulong, VtableSymbol[]> { [0x7000] = [new("CQScanHash", null), new("CXteHash", null)] };

        Assert.Equal(PlanObjectClass.Row, PlanNodeFunctionLookup.GetPlanObjects(vtables)[0x7000]);
    }

    private static PlanNodeFunctionType?[] Types(params string[] names) => [.. names.Select(n => PlanNodeFunctionLookup.GetFunctionType(n))];
}
