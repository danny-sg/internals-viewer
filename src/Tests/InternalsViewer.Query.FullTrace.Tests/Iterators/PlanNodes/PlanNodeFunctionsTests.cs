using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators.PlanNodes;

[Trait("Category", "Unit")]
public class PlanNodeFunctionsTests
{
    [Fact]
    public void Batch_Iterators_Are_Found_By_Their_Factory_And_The_Call_That_Hands_Over_A_Batch()
    {
        var collector = new PlanNodeFunctions.Collector();

        collector.VisitFunction(0x10, "CQScanHash::GetRow");
        collector.VisitFunction(0x20, "CBpQScanFilter::BpGetBatchData");
        collector.VisitFunction(0x30, "CBpQScanFilter::BpOpen");
        collector.VisitFunction(0x40, "CBpXteFilter::PbpqsGet");

        var functions = collector.Build(NoVtables).Functions.OrderBy(f => f.Key).Select(f => (f.Key, f.Value));

        (ulong, PlanNodeFunctionType)[] expected =
        [
            (0x10, PlanNodeFunctionType.Iterator),
            (0x20, PlanNodeFunctionType.BatchIterator),
            (0x40, PlanNodeFunctionType.BatchFactory)
        ];

        Assert.Equal(expected, functions);
    }

    [Fact]
    public void Every_Iterator_Method_Of_The_Batch_Helper_Is_A_Batch_Helper_Call()
    {
        var collector = new PlanNodeFunctions.Collector();

        collector.VisitFunction(0x10, "CQScanBatchHelper::GetRow");
        collector.VisitFunction(0x20, "CQScanBatchHelper::Open");
        collector.VisitFunction(0x30, "CQScanHash::GetRow");

        var functions = collector.Build(NoVtables).Functions.OrderBy(f => f.Key).Select(f => (f.Key, f.Value));

        (ulong, PlanNodeFunctionType)[] expected =
        [
            (0x10, PlanNodeFunctionType.BatchHelper),
            (0x20, PlanNodeFunctionType.BatchHelper),
            (0x30, PlanNodeFunctionType.Iterator)
        ];

        Assert.Equal(expected, functions);
    }

    [Fact]
    public void An_Open_Reached_Through_Its_Helper_Is_An_Iterator_Call()
    {
        var collector = new PlanNodeFunctions.Collector();

        collector.VisitFunction(0x10, "CQScanSortNew::OpenHelper");
        collector.VisitFunction(0x20, "CQScanSortNew::BuildSortTable");

        var functions = collector.Build(NoVtables).Functions.Select(f => (f.Key, f.Value));

        (ulong, PlanNodeFunctionType)[] expected = [(0x10, PlanNodeFunctionType.Iterator)];

        Assert.Equal(expected, functions);
    }

    [Fact]
    public void Constructors_And_The_Query_Teardown_Are_Found_By_Name()
    {
        var collector = new PlanNodeFunctions.Collector();

        collector.VisitFunction(0x10, "CQScanHash::CQScanHash");
        collector.VisitFunction(0x20, "CQueryScan::DestroyQuery");
        collector.VisitFunction(0x30, "CQueryScan::GetRow");

        var functions = collector.Build(NoVtables).Functions.OrderBy(f => f.Key).Select(f => (f.Key, f.Value));

        (ulong, PlanNodeFunctionType)[] expected =
        [
            (0x10, PlanNodeFunctionType.Constructor),
            (0x20, PlanNodeFunctionType.Teardown)
        ];

        Assert.Equal(expected, functions);
    }

    [Fact]
    public void Plan_Objects_Are_Known_By_Their_VTables()
    {
        var vtables = new Dictionary<ulong, string[]>
        {
            [0x7000] = ["CXteHash"],
            [0x7100] = ["CBpXteFilter"],
            [0x7200] = ["CQScanHash"]
        };

        var planObjects = new PlanNodeFunctions.Collector().Build(vtables).PlanObjects.OrderBy(p => p.Key).Select(p => (p.Key, p.Value));

        (ulong, PlanObjectClass)[] expected = [(0x7000, PlanObjectClass.Row), (0x7100, PlanObjectClass.Batch)];

        Assert.Equal(expected, planObjects);
    }

    [Fact]
    public void A_Shared_Vtable_Is_A_Plan_Object_When_Any_Of_Its_Classes_Is()
    {
        var vtables = new Dictionary<ulong, string[]> { [0x7000] = ["CQScanHash", "CXteHash"] };

        Assert.Equal(PlanObjectClass.Row, new PlanNodeFunctions.Collector().Build(vtables).PlanObjects[0x7000]);
    }

    private static Dictionary<ulong, string[]> NoVtables => [];
}
