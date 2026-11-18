using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

namespace InternalsViewer.UI.App.Tests.ViewModels.Query.Trace.Timeline.Memory;

[Trait("Category", "Unit")]
public class MemoryChangeRunsTests
{
    private const int Size = 16;

    [Fact]
    public void A_Change_Takes_The_Function_That_Wrote_It()
    {
        var run = Assert.Single(MemoryChangeRuns.GetRuns([Write(4, 4, 7)], 1, Size));

        Assert.Equal(new MemoryChangeRun(4, 7, MemoryChangeState.Changed, 7), run);
    }

    [Fact]
    public void A_Byte_Takes_The_Last_Function_To_Change_It()
    {
        var runs = MemoryChangeRuns.GetRuns([Write(4, 4, 7), Write(6, 4, 9)], 2, Size);

        Assert.Equal([new MemoryChangeRun(4, 5, MemoryChangeState.Changed, 7),
                      new MemoryChangeRun(6, 9, MemoryChangeState.Changed, 9)],
                     runs);
    }

    [Fact]
    public void A_Change_After_The_Position_Is_Pending()
    {
        var runs = MemoryChangeRuns.GetRuns([Write(0, 2, 7), Write(8, 2, 9)], 1, Size);

        Assert.Equal([new MemoryChangeRun(0, 1, MemoryChangeState.Changed, 7),
                      new MemoryChangeRun(8, 9, MemoryChangeState.Pending, 0)],
                     runs);
    }

    [Fact]
    public void A_Byte_Changed_Again_Later_Keeps_Its_Current_Function()
    {
        var run = Assert.Single(MemoryChangeRuns.GetRuns([Write(0, 4, 7), Write(0, 4, 9)], 1, Size));

        Assert.Equal(MemoryChangeState.Changed, run.State);

        Assert.Equal(7, run.ReplayNodeIndex);
    }

    [Fact]
    public void Nothing_Written_Yet_Leaves_Every_Change_Pending()
    {
        var run = Assert.Single(MemoryChangeRuns.GetRuns([Write(2, 2, 7), Write(4, 2, 9)], 0, Size));

        Assert.Equal(new MemoryChangeRun(2, 5, MemoryChangeState.Pending, 0), run);
    }

    [Fact]
    public void Initialising_Writes_Are_Not_Changes()
    {
        var runs = MemoryChangeRuns.GetRuns([Write(0, 8, 7, MemoryHistoryAccessType.Initialised)], 1, Size);

        Assert.Empty(runs);
    }

    [Fact]
    public void A_Write_Past_The_Block_Is_Cut_At_Its_End()
    {
        var run = Assert.Single(MemoryChangeRuns.GetRuns([Write(12, 8, 7)], 1, Size));

        Assert.Equal(Size - 1, run.End);
    }

    [Fact]
    public void A_Read_Takes_The_Function_That_Last_Read_It()
    {
        var runs = MemoryChangeRuns.GetReadRuns([Read(1, 4, 4, 7), Read(2, 6, 4, 9)], new TracePosition(5, 0), Size);

        Assert.Equal([new MemoryChangeRun(4, 5, MemoryChangeState.Read, 7), new MemoryChangeRun(6, 9, MemoryChangeState.Read, 9)], runs);
    }

    [Fact]
    public void A_Read_After_The_Playhead_Is_Not_Shown()
        => Assert.Empty(MemoryChangeRuns.GetReadRuns([Read(9, 4, 4, 7)], new TracePosition(5, 0), Size));

    private static MemoryHistoryRead Read(ulong sequence, int offset, int length, int replayNodeIndex)
        => new(new TracePosition(sequence, 0), replayNodeIndex, 0, offset, length, 1);

    private static MemoryHistoryWrite Write(int offset,
                                            int length,
                                            int replayNodeIndex,
                                            MemoryHistoryAccessType type = MemoryHistoryAccessType.Changed)
        => new(0, TracePosition.Unknown, replayNodeIndex, 0, offset, (ushort)length, type);
}
