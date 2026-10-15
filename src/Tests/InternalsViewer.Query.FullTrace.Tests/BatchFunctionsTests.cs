using InternalsViewer.Query.FullTrace.Batches;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class BatchFunctionsTests
{
    private static readonly Dictionary<string, string[]> Bases = new()
    {
        ["CBpQScanFilter"] = ["CBpQScanFilter", "CBpQScan"],
        ["CBpQScanColumnStoreScan"] = ["CBpQScanColumnStoreScan", "CBpQScan"],
        ["CBpQScanRowStoreScan"] = ["CBpQScanRowStoreScan", "CBpQScanIndexScan", "CBpQScan"],
        ["CBpQScanTop"] = ["CBpQScanTop", "CBpQScan"],
        ["CBpEmptyScanner"] = ["CBpEmptyScanner", "IBpScanner"],
        ["CQScanTableScanNew"] = ["CQScanTableScanNew", "CQScanRowsetNew", "CQScanNew"]
    };

    [Fact]
    public void BpGetBatchData_Of_Batch_Operators_Is_Read()
    {
        var collector = new BatchFunctions.Collector();

        collector.VisitFunction(0x10, "CBpQScanFilter::BpGetBatchData");

        collector.VisitFunction(0x20, "CBpQScanColumnStoreScan::BpGetBatchData");

        collector.VisitFunction(0x20, "CBpQScanRowStoreScan::BpGetBatchData");

        collector.VisitFunction(0x40, "CBpQScanFilter::BpGetNextBatch");

        collector.VisitFunction(0x50, "CQScanTableScanNew::BpGetBatchData");

        ulong[] functions = [.. collector.Build(Bases).Functions.Order()];

        Assert.Equal([0x10ul, 0x20], functions);
    }

    [Fact]
    public void An_Address_Shared_With_Another_Class_Is_Not_Read()
    {
        var collector = new BatchFunctions.Collector();

        collector.VisitFunction(0x10, "CBpQScanFilter::BpGetBatchData");

        collector.VisitFunction(0x30, "CBpEmptyScanner::BpGetBatchData");

        collector.VisitFunction(0x30, "CBpQScanTop::BpGetBatchData");

        Assert.Equal([0x10ul], collector.Build(Bases).Functions);
    }

    [Fact]
    public void Nothing_Is_Read_Without_A_Batch_Operator()
    {
        var collector = new BatchFunctions.Collector();

        collector.VisitFunction(0x30, "CBpEmptyScanner::BpGetBatchData");

        Assert.Same(BatchFunctions.Empty, collector.Build(Bases));
    }
}
