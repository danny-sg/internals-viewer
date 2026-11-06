using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;
using InternalsViewer.Query.FullTrace.InstructionHistory.Replay;
using InternalsViewer.Query.Recording;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Tests.Replay;

[Trait("Category", "Unit")]
public class InstructionHistoryReaderTests
{
    private static readonly InstructionRequest Request = new(new CallFrame(), 1, new TracePosition(10, 0), new TracePosition(20, 0));

    [Fact]
    public async Task A_Run_Without_A_Recording_Cannot_Be_Replayed()
    {
        var reader = new InstructionHistoryReader();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(null, Request, default));

        Assert.Equal(InstructionHistoryReader.TraceUnavailableMessage, exception.Message);
    }

    [Fact]
    public async Task A_Recording_That_Has_Been_Replaced_Cannot_Be_Replayed()
    {
        var trace = new TimeTravelTraceFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".run"), "TTDReplay.dll");

        var reader = new InstructionHistoryReader();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(trace, Request, default));

        Assert.Equal(InstructionHistoryReader.TraceUnavailableMessage, exception.Message);
    }
}
