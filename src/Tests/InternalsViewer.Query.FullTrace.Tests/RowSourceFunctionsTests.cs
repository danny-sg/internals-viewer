using InternalsViewer.Query.FullTrace.RowSources;
using InternalsViewer.Query.FullTrace.RowSources.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class RowSourceFunctionsTests
{
    [Fact]
    public void Batch_Iterators_Are_Found_By_Their_Factory_And_The_Call_That_Hands_Over_A_Batch()
    {
        var collector = new RowSourceFunctions.Collector();

        collector.VisitFunction(0x10, "CQScanHash::GetRow");
        collector.VisitFunction(0x20, "CBpQScanFilter::BpGetBatchData");
        collector.VisitFunction(0x30, "CBpQScanFilter::BpOpen");
        collector.VisitFunction(0x40, "CBpXteFilter::PbpqsGet");

        var functions = collector.Build().Functions.OrderBy(f => f.Key).Select(f => (f.Key, f.Value));

        (ulong, RowSourceFunctionType)[] expected =
        [
            (0x10, RowSourceFunctionType.Iterator),
            (0x20, RowSourceFunctionType.BatchIterator),
            (0x40, RowSourceFunctionType.BatchFactory)
        ];

        Assert.Equal(expected, functions);
    }

    [Fact]
    public void Every_Iterator_Method_Of_The_Batch_Helper_Is_A_Batch_Helper_Call()
    {
        var collector = new RowSourceFunctions.Collector();

        collector.VisitFunction(0x10, "CQScanBatchHelper::GetRow");
        collector.VisitFunction(0x20, "CQScanBatchHelper::Open");
        collector.VisitFunction(0x30, "CQScanHash::GetRow");

        var functions = collector.Build().Functions.OrderBy(f => f.Key).Select(f => (f.Key, f.Value));

        (ulong, RowSourceFunctionType)[] expected =
        [
            (0x10, RowSourceFunctionType.BatchHelper),
            (0x20, RowSourceFunctionType.BatchHelper),
            (0x30, RowSourceFunctionType.Iterator)
        ];

        Assert.Equal(expected, functions);
    }

    [Fact]
    public void Constructors_And_The_Query_Teardown_Are_Found_By_Name()
    {
        var collector = new RowSourceFunctions.Collector();

        collector.VisitFunction(0x10, "CQScanHash::CQScanHash");
        collector.VisitFunction(0x20, "CQueryScan::DestroyQuery");
        collector.VisitFunction(0x30, "CQueryScan::GetRow");

        var functions = collector.Build().Functions.OrderBy(f => f.Key).Select(f => (f.Key, f.Value));

        (ulong, RowSourceFunctionType)[] expected =
        [
            (0x10, RowSourceFunctionType.Constructor),
            (0x20, RowSourceFunctionType.Teardown)
        ];

        Assert.Equal(expected, functions);
    }

    [Fact]
    public void Plan_Objects_Are_Known_By_Their_VTables()
    {
        var collector = new RowSourceFunctions.Collector();

        collector.VisitData(0x7000, "CXteHash::`vftable'");
        collector.VisitData(0x7100, "CBpXteFilter::`vftable'");
        collector.VisitData(0x7200, "CQScanHash::`vftable'");

        var planObjects = collector.Build().PlanObjects.OrderBy(p => p.Key).Select(p => (p.Key, p.Value));

        (ulong, PlanObjectClass)[] expected = [(0x7000, PlanObjectClass.Row), (0x7100, PlanObjectClass.Batch)];

        Assert.Equal(expected, planObjects);
    }
}
