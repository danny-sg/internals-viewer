using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace;

namespace InternalsViewer.UI.App.Tests.ViewModels.Query.Trace;

[Trait("Category", "Unit")]
public class MemoryChangeNavigationTests
{
    private static readonly MemoryHistoryWrite[] Writes =
    [
        Write(10, 0, 8),
        Write(20, 4, 2),
        Write(30, 0, 4, MemoryHistoryChangeType.Initialised),
        Write(40, 5, 1),
        Write(50, 12, 4)
    ];

    [Fact]
    public void Next_Is_The_First_Change_To_The_Byte_After_The_Playhead()
    {
        Assert.Equal(1, MemoryChangeNavigation.GetNextChange(Writes, 5, At(15)));
    }

    [Fact]
    public void Next_From_A_Change_Moves_On_To_The_One_After()
    {
        var first = MemoryChangeNavigation.GetNextChange(Writes, 5, At(0));

        var second = MemoryChangeNavigation.GetNextChange(Writes, 5, Writes[first].Position);

        var third = MemoryChangeNavigation.GetNextChange(Writes, 5, Writes[second].Position);

        Assert.Equal(new[] { 0, 1, 3 }, new[] { first, second, third });
    }

    [Fact]
    public void Next_Past_The_Last_Change_Finds_Nothing()
    {
        Assert.Equal(-1, MemoryChangeNavigation.GetNextChange(Writes, 5, At(40)));
    }

    [Fact]
    public void Previous_From_A_Change_Moves_Back_To_The_One_Before()
    {
        var first = MemoryChangeNavigation.GetPreviousChange(Writes, 5, At(60));

        var second = MemoryChangeNavigation.GetPreviousChange(Writes, 5, Writes[first].Position);

        var third = MemoryChangeNavigation.GetPreviousChange(Writes, 5, Writes[second].Position);

        Assert.Equal(new[] { 3, 1, 0 }, new[] { first, second, third });
    }

    [Fact]
    public void Previous_Before_The_First_Change_Finds_Nothing()
    {
        Assert.Equal(-1, MemoryChangeNavigation.GetPreviousChange(Writes, 5, At(10)));
    }

    [Fact]
    public void Writes_That_Miss_The_Byte_Are_Skipped()
    {
        Assert.Equal(4, MemoryChangeNavigation.GetNextChange(Writes, 13, At(0)));

        Assert.Equal(-1, MemoryChangeNavigation.GetNextChange(Writes, 9, At(0)));
    }

    [Fact]
    public void Initialising_Writes_Are_Not_Changes()
    {
        Assert.Equal(-1, MemoryChangeNavigation.GetNextChange(Writes, 0, At(10)));

        Assert.Equal(0, MemoryChangeNavigation.GetPreviousChange(Writes, 0, At(35)));
    }

    private static TracePosition At(ulong sequence) => new(sequence, 0);

    private static MemoryHistoryWrite Write(ulong sequence,
                                            int offset,
                                            int length,
                                            MemoryHistoryChangeType type = MemoryHistoryChangeType.Changed)
        => new(At(sequence), 0, 0, 0, offset, 0, (ushort)length, type);
}
