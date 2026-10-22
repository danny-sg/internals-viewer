using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.UI.App.ViewModels.Query.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.MemoryHistory.Enums;

namespace InternalsViewer.UI.App.Tests.ViewModels.Query.MemoryHistory;

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

        Assert.Equal(7, run.CallNodeIndex);
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
        var runs = MemoryChangeRuns.GetRuns([Write(0, 8, 7, MemoryHistoryChangeType.Initialised)], 1, Size);

        Assert.Empty(runs);
    }

    [Fact]
    public void A_Write_Past_The_Block_Is_Cut_At_Its_End()
    {
        var run = Assert.Single(MemoryChangeRuns.GetRuns([Write(12, 8, 7)], 1, Size));

        Assert.Equal(Size - 1, run.End);
    }

    private static MemoryHistoryWrite Write(int offset,
                                            int length,
                                            int callNodeIndex,
                                            MemoryHistoryChangeType type = MemoryHistoryChangeType.Changed)
        => new(TracePosition.Unknown, 0, callNodeIndex, 0, offset, 0, (ushort)length, type);
}
