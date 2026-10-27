using InternalsViewer.Mcp.Tools;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tests.Tools;

[Trait("Category", "Unit")]
public class RangeToolsTests
{
    [Fact]
    public async Task Events_In_A_Window_Keep_Only_Those_Overlapping_It()
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create());

        var all = await new EventTools(resolver).Events();

        var window = await new EventTools(resolver).Events(fromMs: 0.65, toMs: 0.70);

        Assert.StartsWith("7 events", all);
        Assert.StartsWith("5 events", window);
        Assert.Contains("ReadEventGroup", window);
        Assert.DoesNotContain("LatchEvent", window);
    }

    [Fact]
    public async Task Reads_In_A_Window_Count_Only_Those_Reads()
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create());

        var text = await new EventTools(resolver).Reads(fromMs: 0.65, toMs: 0.70);

        Assert.Contains("1 page reads", text);
    }

    [Fact]
    public async Task Timeline_Spans_On_The_Position_Axis_Keep_Only_Those_In_Range()
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create());

        var all = await new TimelineTools(resolver).Timeline(top: 5, axis: "position");

        var range = await new TimelineTools(resolver).Timeline(top: 5, axis: "position", from: "1E:0", to: "23:0");

        Assert.Contains("of 3 spans", all);
        Assert.Contains("longest 2 of 2 spans", range);
    }

    [Fact]
    public async Task Timeline_Spans_On_The_Instructions_Axis_Take_Instruction_Counts()
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create());

        var range = await new TimelineTools(resolver).Timeline(top: 5, from: "40", to: "200");

        Assert.Contains("longest 1 of 1 spans", range);
    }

    [Fact]
    public async Task A_Position_That_Is_Not_Hex_Sequence_And_Steps_Is_Refused()
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create());

        await Assert.ThrowsAsync<McpException>(() => new TimelineTools(resolver).Timeline(top: 1, axis: "position", from: "12.5"));
    }

    [Fact]
    public async Task Calls_In_A_Position_Range_Keep_Only_Those_Starting_In_It()
    {
        var (resolver, _) = TestRuns.CreateResolver(TestRuns.Create());

        var text = await new CallLogTools(resolver).Calls(function: "GetRow", from: "2:0");

        Assert.DoesNotContain("#0 ", text);
        Assert.Contains("#1 ", text);
    }
}
