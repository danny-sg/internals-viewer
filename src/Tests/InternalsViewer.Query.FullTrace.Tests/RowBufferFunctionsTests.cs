using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class RowBufferFunctionsTests
{
    private static readonly Dictionary<string, string[]> Bases = new()
    {
        ["CQScanTableScanNew"] = ["CQScanTableScanNew", "CQScanRowsetDynaNew", "CQScanRowsetLeafNew", "CQScanRowsetNew", "CQScanNew"],
        ["CQScanSortNew"] = ["CQScanSortNew", "CQScanRowsetNew", "CQScanNew"],
        ["CQScanRowsetNew"] = ["CQScanRowsetNew", "CQScanNew"],
        ["CQScanHashNew"] = ["CQScanHashNew", "CQScanNew"],
        ["CBpQScanRowStoreScan"] = ["CBpQScanRowStoreScan", "CBpQScanIndexScan", "CBpQScan"]
    };

    [Fact]
    public void GetRow_Methods_Of_Rowset_Iterators_And_The_Helper_Are_Read()
    {
        var collector = Collect();

        ulong[] functions = [.. collector.Build(Bases).Reads.Select(r => r.Function).Order()];

        Assert.Equal([0x10ul, 0x20, 0x30, 0x40, 0x50], functions);
    }

    [Fact]
    public void Every_Rowset_Iterator_Has_A_Layout()
    {
        var collector = Collect();

        ulong[] vtables = [.. collector.Build(Bases).Layouts.Select(l => l.VTable).Order()];

        Assert.Equal([0x1000ul, 0x2000, 0x3000], vtables);
    }

    [Fact]
    public void Layouts_Hold_The_Rowset_Buffer_And_Bindings()
    {
        var layout = Collect().Build(Bases).Layouts.Single(l => l.VTable == 0x1000);

        Assert.Equal(new RowBufferLayout(0x1000, 0x80, 0x88), layout);
    }

    [Fact]
    public void The_Batch_Helper_Reads_Its_Value_Row()
    {
        var collector = new RowBufferFunctions.Collector();

        collector.VisitFunction(0xA0, "CQScanBatchHelper::GetRow");

        collector.VisitData(0x7000, "CQScanBatchHelper::`vftable'");

        var functions = collector.Build(new Dictionary<string, string[]> { ["CQScanBatchHelper"] = ["CQScanBatchHelper", "CQScanNew"] });

        Assert.Equal(0xA0ul, Assert.Single(functions.Reads).Function);
        Assert.Equal(new RowBufferLayout(0x7000, 0x78, 0, 0x50, RowBufferFormat.Values), Assert.Single(functions.Layouts));
    }

    [Fact]
    public void Class_Hierarchies_Are_Collected_By_Class()
    {
        var collector = Collect();

        Assert.Equal(0x9000u, collector.Hierarchies["CQScanTableScanNew"]);
    }

    [Fact]
    public void Nothing_Is_Read_Without_A_Rowset_Iterator()
    {
        var collector = new RowBufferFunctions.Collector();

        collector.VisitFunction(0x10, "CQScanHashNew::GetRow");

        collector.VisitData(0x1000, "CQScanHashNew::`vftable'");

        Assert.Same(RowBufferFunctions.Empty, collector.Build(Bases));
    }

    private static RowBufferFunctions.Collector Collect()
    {
        var collector = new RowBufferFunctions.Collector();

        collector.VisitFunction(0x10, "CQScanTableScanNew::GetRow");
        collector.VisitFunction(0x20, "CQScanTableScanNew::GetRowDirectSelect");
        collector.VisitFunction(0x30, "CQScanSortNew::GetRow");
        collector.VisitFunction(0x40, "CQScanRowsetNew::GetRow");
        collector.VisitFunction(0x50, "CQScanNew::GetRowOrReQualifyHelper");
        collector.VisitFunction(0x60, "CQScanRowsetNew::GetRowWithPrefetch");
        collector.VisitFunction(0x70, "CQScanHashNew::GetRow");
        collector.VisitFunction(0x80, "CBpQScanRowStoreScan::GetRow");
        collector.VisitFunction(0x90, "CQScanTableScanNew::Open");

        collector.VisitData(0x1000, "CQScanTableScanNew::`vftable'");
        collector.VisitData(0x2000, "CQScanSortNew::`vftable'");
        collector.VisitData(0x3000, "CQScanRowsetNew::`vftable'");
        collector.VisitData(0x4000, "CQScanHashNew::`vftable'");
        collector.VisitData(0x5000, "CBpQScanRowStoreScan::`vftable'");
        collector.VisitData(0x6000, "CQScanTableScanNew::`RTTI Complete Object Locator'");
        collector.VisitData(0x9000, "CQScanTableScanNew::`RTTI Class Hierarchy Descriptor'");

        return collector;
    }
}
