using InternalsViewer.Mcp.Tools;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Output;
using ModelContextProtocol;
using static InternalsViewer.Mcp.Tests.TestRuns;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class TimelineToolsTests
{
    [Fact]
    public async Task Timeline_Lists_The_Threads()
    {
        var (resolver, _) = CreateResolver(Create());

        var text = await new TimelineTools(resolver).Timeline();

        Assert.Contains("steps per sequence, start 0:0 end 64:0", text);
        Assert.Contains("Thread  Calls  Spans", text);
        Assert.Contains("64:0", text);
        Assert.DoesNotContain("longest", text);
    }

    [Fact]
    public async Task Timeline_Ranks_The_Longest_Spans_By_Instructions()
    {
        var (resolver, _) = CreateResolver(Create());

        var text = await new TimelineTools(resolver).Timeline(top: 2);

        Assert.Contains("longest 2 of 3 spans by instructions:", text);

        var lines = text.Split(Environment.NewLine);

        var first = Assert.Single(lines, l => l.Contains("sqlmin!CQueryScan::GetRow"));

        Assert.StartsWith("100", first);
        Assert.Contains("ret", first);
        Assert.Contains("0:0", first);
        Assert.Contains("64:0", first);
        Assert.Contains("sqlmin!CQScanRowsetNew::GetRow", text);
    }

    [Fact]
    public async Task Timeline_Filters_The_Longest_Spans_By_Thread_And_Depth()
    {
        var (resolver, _) = CreateResolver(Create());

        var text = await new TimelineTools(resolver).Timeline(top: 1, thread: 7, depth: 1);

        Assert.Contains("longest 1 of 2 spans", text);
        Assert.Contains("sqlmin!CQScanRowsetNew::GetRow", text);
        Assert.DoesNotContain("sqlmin!CQueryScan::GetRow", text.Split("longest")[1]);
    }

    [Fact]
    public async Task Timeline_Totals_The_Instructions_Of_One_Node()
    {
        var (resolver, _) = CreateResolver(Create());

        var text = await new TimelineTools(resolver).Timeline(node: 2);

        Assert.Contains("node 2 sqlmin!CQScanRowsetNew::GetRow: 2 spans, 10 instructions including callees", text);
    }

    [Fact]
    public async Task Timeline_Rejects_An_Unknown_Axis()
    {
        var (resolver, _) = CreateResolver(Create());

        await Assert.ThrowsAsync<McpException>(() => new TimelineTools(resolver).Timeline(top: 1, axis: "time"));
    }

    [Fact]
    public async Task Timeline_Needs_A_Full_Trace()
    {
        var (resolver, _) = CreateResolver(Create(withTrace: false));

        await Assert.ThrowsAsync<McpException>(() => new TimelineTools(resolver).Timeline());
    }

    [Fact]
    public async Task Plan_Nodes_Reports_When_There_Are_None()
    {
        var (resolver, _) = CreateResolver(Create());

        Assert.Equal("no plan nodes in the trace", await new TimelineTools(resolver).PlanNodes());
    }

    [Fact]
    public async Task Plan_Nodes_Lists_The_Iterator_Objects_With_Their_Operator()
    {
        var run = WithTrace(Create(), t => t with
        {
            PlanNodes = [new IteratorPlanNode(Scan, 0x1234, 0, 0x5678, 0, new TracePoint(At(1), 5))]
        });

        var (resolver, _) = CreateResolver(run);

        var text = await new TimelineTools(resolver).PlanNodes();

        Assert.Contains("plan nodes: 1 iterator objects linked to plan nodes", text);
        Assert.Contains("Clustered Index Scan", text);
        Assert.Contains("0x9000", text);
        Assert.Contains("0x1234", text);
        Assert.Contains("0x5678", text);
        Assert.Contains("1:0", text);
    }

    [Fact]
    public async Task Teardowns_Reports_When_There_Are_None()
    {
        var (resolver, _) = CreateResolver(Create());

        Assert.Equal("no teardowns in the trace", await new TimelineTools(resolver).Teardowns());
    }

    [Fact]
    public async Task Teardowns_Lists_Where_Each_Thread_Ended()
    {
        var run = WithTrace(Create(), t => t with { Teardowns = [new QueryTeardown(7, new TracePoint(At(0x64), 100))] });

        var (resolver, _) = CreateResolver(run);

        var text = await new TimelineTools(resolver).Teardowns();

        Assert.Contains("Thread  Position  Instructions", text);
        Assert.Contains("64:0", text);
        Assert.Contains("100", text);
    }

    [Fact]
    public async Task Output_Reports_When_There_Are_No_Structures()
    {
        var (resolver, _) = CreateResolver(Create());

        Assert.Equal("no output structures in the trace", await new TimelineTools(resolver).Output());
    }

    [Fact]
    public async Task Output_Lists_The_Owner_Of_Each_Column()
    {
        var run = WithTrace(Create(), t => t with { OutputStructures = [new TraceOutputStructure(0x1000, [Scan, 0x9001], Scan)] });

        var (resolver, _) = CreateResolver(run);

        var text = await new TimelineTools(resolver).Output();

        Assert.Contains("output structures: 1 output rows", text);
        Assert.Contains("0x1000", text);
        Assert.Contains("0x9000, 0x9001", text);
    }

    private static QueryExportContent WithTrace(QueryExportContent run, Func<TraceResult, TraceResult> change)
        => run with { Extras = [change(run.ExtraOf<TraceResult>()!)] };
}
