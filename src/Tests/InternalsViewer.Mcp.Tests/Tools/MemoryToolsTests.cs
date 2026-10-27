using InternalsViewer.Mcp.Tools;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.Memory;
using ModelContextProtocol;
using static InternalsViewer.Mcp.Tests.TestRuns;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class MemoryToolsTests
{
    [Fact]
    public async Task Memory_Lists_Each_Thread_And_The_Peak_In_Use()
    {
        var (resolver, _) = CreateResolver(Create());

        var text = await new MemoryTools(resolver).Memory();

        Assert.Contains("Thread  Allocations  Allocated  Freed", text);
        Assert.Contains("100 B", text);
        Assert.Contains("in use: peak 100 B", text);
        Assert.DoesNotContain("memory map", text);
    }

    [Fact]
    public async Task Memory_Needs_A_Full_Trace()
    {
        var (resolver, _) = CreateResolver(Create(withTrace: false));

        await Assert.ThrowsAsync<McpException>(() => new MemoryTools(resolver).Memory());
    }

    [Fact]
    public async Task Memory_Map_Reports_When_There_Is_None()
    {
        var (resolver, _) = CreateResolver(Create());

        Assert.Equal("no memory map in the trace", await new MemoryTools(resolver).MemoryMap());
    }

    [Fact]
    public async Task Memory_Map_Groups_By_Class_And_Lists_The_Matching_Entries()
    {
        var (resolver, _) = CreateResolver(WithMemoryMap(Create()));

        var text = await new MemoryTools(resolver).MemoryMap(grep: "CQScanRowsetNew");

        Assert.Contains("memory map: 2 entries; Allocated 1, Constructed 1", text);
        Assert.Contains("Class or site", text);
        Assert.Contains("CQScanRowsetNew", text);
        Assert.Contains("2 entries matching:", text);
        Assert.Contains("0xA 100 B Allocated CQScanRowsetNew sqlmin!CQScanRowsetNew::GetRow thread 7 16:0 to 1F:0 owner 0x0 in sqlmin!CQScanRowsetNew::GetRow", text);
        Assert.Contains("0xA 100 B Constructed CQScanRowsetNew", text);
    }

    [Fact]
    public async Task Memory_Map_Lists_The_Entries_Of_One_Address_Grouped_By_Operation()
    {
        var (resolver, _) = CreateResolver(WithMemoryMap(Create()));

        var text = await new MemoryTools(resolver).MemoryMap(address: "0xA", by: "operation");

        Assert.Matches(@"Operation\s+Entries\s+Blocks\s+Bytes", text);
        Assert.Matches(@"Allocated\s+1\s+1\s+100 B", text);
        Assert.Matches(@"Constructed\s+1\s+1\s+100 B", text);
        Assert.Contains("2 entries matching:", text);
    }

    [Fact]
    public async Task Memory_Map_Rejects_An_Unknown_Grouping()
    {
        var (resolver, _) = CreateResolver(WithMemoryMap(Create()));

        await Assert.ThrowsAsync<McpException>(() => new MemoryTools(resolver).MemoryMap(by: "thread"));
    }

    [Fact]
    public async Task Memory_Includes_The_Memory_Map_Summary_When_There_Is_One()
    {
        var (resolver, _) = CreateResolver(WithMemoryMap(Create()));

        var text = await new MemoryTools(resolver).Memory();

        Assert.Contains("in use: peak 100 B", text);
        Assert.Contains("memory map: 2 entries", text);
    }

    [Fact]
    public async Task Object_Pool_Reports_When_There_Is_No_Snapshot()
    {
        var (resolver, _) = CreateResolver(Create());

        Assert.Equal("no object pool snapshot in the trace", await new MemoryTools(resolver).ObjectPool());
    }

    [Fact]
    public async Task Object_Pool_Lists_The_Entries()
    {
        var pool = new ObjectPoolSnapshot([new ObjectPoolEntry(0x10, 0x20, 0x30, 8, ColumnStoreObjectType.ColumnSegment, 5, 72057594, 1, 2, 4096)]);

        var (resolver, _) = CreateResolver(WithTrace(Create(), t => t with { ObjectPool = pool }));

        var text = await new MemoryTools(resolver).ObjectPool();

        Assert.Contains("1 columnstore object pool entries at the end of the run", text);
        Assert.Contains("ColumnSegment", text);
        Assert.Contains("72057594", text);
        Assert.Contains("4.0 KB", text);
        Assert.Contains("0x20", text);
    }

    [Fact]
    public async Task Memory_History_Reports_When_There_Is_None()
    {
        var (resolver, _) = CreateResolver(Create());

        Assert.Equal("no memory history in the trace", await new MemoryTools(resolver).MemoryHistory());
    }

    [Fact]
    public async Task Memory_History_Lists_The_Blocks()
    {
        var (resolver, _) = CreateResolver(WithMemoryHistory(Create()));

        var text = await new MemoryTools(resolver).MemoryHistory();

        Assert.Contains("memory history: 1 watched blocks, 2 writes", text);
        Assert.Contains("1 blocks selected", text);
        Assert.Contains("#  Address  Size  Kind      Iterator  Thread  From  To    Writes  Allocated in", text);
        Assert.Contains("0  0xA      16    iterator  0x9000    7       16:0  1F:0  2       sqlmin!CQScanRowsetNew::GetRow", text);
        Assert.DoesNotContain("block 0", text);
    }

    [Fact]
    public async Task Memory_History_Filters_The_Blocks()
    {
        var (resolver, _) = CreateResolver(WithMemoryHistory(Create()));

        var tools = new MemoryTools(resolver);

        Assert.Contains("0 blocks selected", await tools.MemoryHistory(kind: "inline"));
        Assert.Contains("1 blocks selected", await tools.MemoryHistory(kind: "iterator", iterator: "0x9000"));
        Assert.Contains("1 blocks selected", await tools.MemoryHistory(address: "0x12"));
        Assert.Contains("0 blocks selected", await tools.MemoryHistory(address: "0x1A"));
        Assert.Contains("1 blocks selected", await tools.MemoryHistory(allocatedIn: "CQScanRowsetNew::"));
        Assert.Contains("0 blocks selected", await tools.MemoryHistory(allocatedIn: "CBpBatch::"));

        await Assert.ThrowsAsync<McpException>(() => tools.MemoryHistory(kind: "batch"));
    }

    [Fact]
    public async Task Memory_History_Shows_The_Writes_Of_One_Block()
    {
        var (resolver, _) = CreateResolver(WithMemoryHistory(Create()));

        var text = await new MemoryTools(resolver).MemoryHistory(block: 0);

        Assert.Contains("block 0 0xA size 16: 2 writes by 1 functions", text);
        Assert.Contains("writers: sqlmin!CQScanRowsetNew::GetRow x2", text);
        Assert.Contains("most written fields (offset/length): +0x0/8 x1, +0x8/4 x1", text);
        Assert.Contains("16:1 +0x0 Initialised 0807060504030201 (= 0x102030405060708) by sqlmin!CQScanRowsetNew::GetRow call 1", text);
        Assert.Contains("17:0 +0x8 Changed 2a000000 (= 0x2A) by sqlmin!CQScanRowsetNew::GetRow call 1", text);
    }

    [Fact]
    public async Task Memory_History_Follows_One_Field()
    {
        var (resolver, _) = CreateResolver(WithMemoryHistory(Create()));

        var text = await new MemoryTools(resolver).MemoryHistory(block: 0, field: "0x8");

        Assert.DoesNotContain("Initialised", text);
        Assert.Contains("17:0 +0x8 Changed 2a000000", text);
    }

    [Fact]
    public async Task Memory_History_Rejects_An_Unknown_Block()
    {
        var (resolver, _) = CreateResolver(WithMemoryHistory(Create()));

        await Assert.ThrowsAsync<McpException>(() => new MemoryTools(resolver).MemoryHistory(block: 3));
    }

    private static QueryExportContent WithMemoryMap(QueryExportContent run)
        => WithTrace(run, t => t with
        {
            MemoryMap = MemoryMap.Build(
            [
                new AddressEntry(0xA, 0, 1, 0, At(0x16), At(0x1F), 1, 7, MemoryMapOperation.Allocated, "CQScanRowsetNew", 100, "sqlmin!CQScanRowsetNew::GetRow"),
                new AddressEntry(0xA, 0, 1, 0, At(0x17), At(0x1F), 1, 7, MemoryMapOperation.Constructed, "CQScanRowsetNew")
            ], t.Timeline.NodeOf)
        });

    private static QueryExportContent WithMemoryHistory(QueryExportContent run)
    {
        MemoryHistoryBlock[] blocks = [new MemoryHistoryBlock(Scan, 0xA, 16, true, false, 7, 1, At(0x16), At(0x1F), 22, 31, 0, 2)];

        MemoryHistoryWrite[] writes =
        [
            new MemoryHistoryWrite(At(0x16, 1), 23, 1, 1, 0, 0, 8, MemoryHistoryChangeType.Initialised),
            new MemoryHistoryWrite(At(0x17), 24, 1, 1, 8, 8, 4, MemoryHistoryChangeType.Changed)
        ];

        byte[] data = [0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x2A, 0x00, 0x00, 0x00];

        var history = new MemoryHistoryStore(blocks, writes, HighWordRuns.Empty, data, new Dictionary<int, ulong> { [1] = Scan });

        return WithTrace(run, t => t with { MemoryHistory = history });
    }

    private static QueryExportContent WithTrace(QueryExportContent run, Func<TraceResult, TraceResult> change)
        => run with { Extras = [change(run.ExtraOf<TraceResult>()!)] };
}
