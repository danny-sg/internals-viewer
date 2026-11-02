using InternalsViewer.Mcp.Tools.Queries;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using ModelContextProtocol;
using static InternalsViewer.Mcp.Tests.TestRuns;

namespace InternalsViewer.Mcp.Tests.Tools.Queries;

[Trait("Category", "Unit")]
public class MemoryWatchToolsTests
{
    [Fact]
    public async Task Memory_Watch_Adds_A_Temporary_Watch_The_Size_Of_Its_Type()
    {
        var (resolver, document) = CreateResolver(WithMemoryHistory(Create()));

        var text = await new MemoryWatchTools(resolver).Watch("add", block: 0, offset: "0x8", type: "int", label: "Rows Returned");

        Assert.Equal(["AddWatch 0 0x8 4 Int Rows Returned"], document.Recorded.Calls);
        Assert.Contains("Watching Rows Returned on Object (block 0), 4 bytes as Int", text);
    }

    [Fact]
    public async Task Memory_Watch_Rejects_A_Field_Outside_The_Block()
    {
        var (resolver, _) = CreateResolver(WithMemoryHistory(Create()));

        var tools = new MemoryWatchTools(resolver);

        await Assert.ThrowsAsync<McpException>(() => tools.Watch("add", block: 0, offset: "0xE", type: "int"));
        await Assert.ThrowsAsync<McpException>(() => tools.Watch("add", block: 0, offset: "0", type: "int", length: 2));
        await Assert.ThrowsAsync<McpException>(() => tools.Watch("add", block: 0, offset: "0"));
    }

    [Fact]
    public async Task Memory_Watch_Clear_Says_When_None_Were_Added()
    {
        var (resolver, _) = CreateResolver(WithMemoryHistory(Create()));

        Assert.Equal("There were no watches added here to clear.", await new MemoryWatchTools(resolver).Watch("clear"));
    }

    [Fact]
    public async Task Query_Object_Graph_Lists_The_Block_It_Starts_From()
    {
        var (resolver, _) = CreateResolver(WithMemoryHistory(Create()));

        var text = await new ObjectGraphTools(resolver).ObjectGraph(block: 0);

        Assert.Contains("object graph from block 0 at 16:0: 1 objects", text);
        Assert.Contains("0xA", text);
    }

    private static QueryExportContent WithMemoryHistory(QueryExportContent run)
    {
        MemoryHistoryBlock[] blocks = [new MemoryHistoryBlock(Scan, 0xA, 16, true, false, 7, 1, At(0x16), At(0x1F), 22, 31, 0, 1)];

        MemoryHistoryWrite[] writes = [new MemoryHistoryWrite(At(0x16, 1), 23, 1, 1, 0, 0, 8, MemoryHistoryChangeType.Initialised)];

        var history = new MemoryHistoryStore(blocks, writes, HighWordRuns.Empty, new byte[8], new Dictionary<int, ulong>());

        return run with { Extras = [run.ExtraOf<TraceResult>()! with { MemoryHistory = history }] };
    }
}
