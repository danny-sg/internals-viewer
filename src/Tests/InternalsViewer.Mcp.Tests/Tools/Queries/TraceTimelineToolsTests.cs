using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Mcp.Models.Context.Queries.Enums;
using InternalsViewer.Mcp.Tests.Documents;
using InternalsViewer.Mcp.Tools.Queries;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using ModelContextProtocol;
using static InternalsViewer.Mcp.Tests.TestRuns;

namespace InternalsViewer.Mcp.Tests.Tools.Queries;

[Trait("Category", "Unit")]
public class TraceTimelineToolsTests
{
    [Fact]
    public async Task Trace_Pin_Pins_Positions_On_The_Timeline()
    {
        var timeline = new RecordingTimeline();

        var text = await Create(timeline).Pin(positions: ["1E:0", "14:0"]);

        Assert.Equal([At(0x1E), At(0x14)], timeline.Pinned!.Select(p => p.Position));
        Assert.Contains("Pinned 2 positions on the Trace Timeline, from 14:0 to 1E:0.", text);
    }

    [Fact]
    public async Task Trace_Pin_Without_A_Function_Or_Positions_Clears_The_Pins()
    {
        var timeline = new RecordingTimeline();

        var text = await Create(timeline, new McpTraceTimeline { PinnedPositions = 3 }).Pin();

        Assert.Empty(timeline.Pinned!);
        Assert.Equal("Cleared 3 pins.", text);
    }

    [Fact]
    public async Task Trace_Pin_Rejects_A_Position_That_Is_Not_Sequence_And_Steps()
    {
        var timeline = new RecordingTimeline();

        await Assert.ThrowsAsync<McpException>(() => Create(timeline).Pin(positions: ["12"]));

        Assert.Null(timeline.Pinned);
    }

    [Fact]
    public async Task Trace_Pin_Reports_A_Function_That_Is_Not_In_The_Call_Graph()
    {
        var timeline = new RecordingTimeline();

        var text = await Create(timeline).Pin("NoSuchFunctionAnywhere");

        Assert.Null(timeline.Pinned);
        Assert.Contains("No function in the call graph matches", text);
    }

    [Fact]
    public async Task Trace_Indicate_Wait_Says_The_User_Pressed_Next()
    {
        var timeline = new RecordingTimeline { Close = new McpIndicatorResult(McpIndicatorClose.Next, 0, 1) };

        var id = Guid.NewGuid();

        var text = await Create(timeline).IndicateWait(id.ToString(), seconds: 20);

        Assert.Equal((id, TimeSpan.FromSeconds(20)), timeline.Waited);
        Assert.Equal("The user pressed Next. Go on to the next step.", text);
    }

    [Fact]
    public async Task Trace_Indicate_Wait_Is_Bounded_And_Says_When_It_Is_Still_Open()
    {
        var timeline = new RecordingTimeline();

        var text = await Create(timeline).IndicateWait(Guid.NewGuid().ToString(), seconds: 600);

        Assert.Equal(TimeSpan.FromSeconds(50), timeline.Waited?.Timeout);
        Assert.Contains("Still open after 50 seconds. Call trace_indicate_wait", text);
    }

    [Theory]
    [InlineData(McpIndicatorClose.Stopped, 1, 4, "The user closed the walkthrough at step 2 of 4.")]
    [InlineData(McpIndicatorClose.Stopped, 3, 4, "reached the last of the 4 steps and closed it. The walkthrough is done.")]
    [InlineData(McpIndicatorClose.Next, 3, 4, "The user went through all 4 steps.")]
    [InlineData(McpIndicatorClose.Stopped, 0, 1, "The user closed it.")]
    public async Task Trace_Indicate_Wait_Says_How_Far_A_Walkthrough_Got(McpIndicatorClose reason, int step, int steps, string expected)
    {
        var timeline = new RecordingTimeline { Close = new McpIndicatorResult(reason, step, steps) };

        var text = await Create(timeline).IndicateWait(Guid.NewGuid().ToString());

        Assert.Contains(expected, text);
    }

    [Fact]
    public async Task Trace_Walkthrough_Needs_Steps_Within_The_Limit()
    {
        var tools = Create(new RecordingTimeline());

        var step = new TraceTimelineTools.WalkthroughStep([new TraceTimelineTools.IndicatorTarget("GetRow")], "Here");

        await Assert.ThrowsAsync<McpException>(() => tools.Walkthrough([]));
        await Assert.ThrowsAsync<McpException>(() => tools.Walkthrough([.. Enumerable.Repeat(step, 21)]));
    }

    [Fact]
    public async Task Trace_Walkthrough_Reports_A_Step_With_A_Function_That_Is_Not_In_The_Call_Graph()
    {
        var step = new TraceTimelineTools.WalkthroughStep([new TraceTimelineTools.IndicatorTarget("NoSuchFunctionAnywhere")], "Here");

        var exception = await Assert.ThrowsAsync<McpException>(() => Create(new RecordingTimeline()).Walkthrough([step]));

        Assert.Contains("No function in the call graph matches", exception.Message);
    }

    [Fact]
    public async Task Trace_Indicate_Wait_Needs_An_Indicator_Id()
        => await Assert.ThrowsAsync<McpException>(() => Create(new RecordingTimeline()).IndicateWait("not-an-id"));

    [Fact]
    public async Task Trace_Indicate_Clear_Says_Whether_One_Was_Open()
    {
        Assert.Equal("Cleared the indicator.", await Create(new RecordingTimeline { HasIndicator = true }).IndicateClear());
        Assert.Equal("No indicator was open.", await Create(new RecordingTimeline()).IndicateClear());
    }

    [Fact]
    public async Task Trace_Indicate_Needs_A_Target_And_A_Comment()
    {
        var tools = Create(new RecordingTimeline());

        await Assert.ThrowsAsync<McpException>(() => tools.Indicate([], "Here"));
        await Assert.ThrowsAsync<McpException>(() => tools.Indicate([new TraceTimelineTools.IndicatorTarget("GetRow")], " "));
    }

    [Fact]
    public async Task Trace_Indicate_Reports_A_Function_That_Is_Not_In_The_Call_Graph()
    {
        var exception = await Assert.ThrowsAsync<McpException>(
            () => Create(new RecordingTimeline()).Indicate([new TraceTimelineTools.IndicatorTarget("NoSuchFunctionAnywhere")], "Here"));

        Assert.Contains("No function in the call graph matches", exception.Message);
    }

    private static TraceTimelineTools Create(RecordingTimeline timeline, McpTraceTimeline? state = null)
    {
        var document = new TestDocument { Run = TestRuns.Create(), TimelineCommands = timeline, TimelineState = state };

        return new TraceTimelineTools(new McpDocumentResolver(new TestDocumentSource(document)));
    }

    private sealed class RecordingTimeline : ITraceTimelineCommands
    {
        public TracePoint[]? Pinned { get; private set; }

        public Task PinPositionsAsync(TracePoint[] points)
        {
            Pinned = points;

            return Task.CompletedTask;
        }

        public Task SetTimelineOptionAsync(string key, bool enabled) => throw new NotSupportedException();

        public Task AddTrackAsync(McpTrackRequest request) => throw new NotSupportedException();

        public Task SetTrackVisibleAsync(string name, bool isVisible) => throw new NotSupportedException();

        public Task<int> RemoveTracksAsync(string? name) => throw new NotSupportedException();

        public Task StepAsync(McpStepMode mode, bool forward, int count) => throw new NotSupportedException();

        public Task SearchAsync(string? text) => throw new NotSupportedException();

        public Task StepSearchAsync(bool forward, int count) => throw new NotSupportedException();

        public Task SelectCallAsync(CallStackNode node, int callIndex, bool zoom) => throw new NotSupportedException();

        public Task ZoomAsync(bool toSelection) => throw new NotSupportedException();

        public McpIndicatorResult? Close { get; set; }

        public bool HasIndicator { get; set; }

        public (Guid Id, TimeSpan Timeout)? Waited { get; private set; }

        public Task<Guid> ShowIndicatorAsync(McpIndicatorRequest request) => throw new NotSupportedException();

        public Task<McpIndicatorResult?> WaitForIndicatorAsync(Guid id, TimeSpan timeout)
        {
            Waited = (id, timeout);

            return Task.FromResult(Close);
        }

        public Task<bool> ClearIndicatorAsync() => Task.FromResult(HasIndicator);
    }
}
