using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Queries;
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
    }
}
