using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Executors;

namespace InternalsViewer.Execution.Tests.UnitTests.Executors;

public class AccessCounterTests
{
    [Fact]
    public void Every_Step_Carries_The_Totals_As_They_Stood()
    {
        var steps = Execute(TestIndexPage.Create(1, 2, 3), new IndexPageWalk());

        Assert.Equal(1, steps[0].Counters.PagesRead);

        Assert.Equal(3, steps[^1].Counters.RowsRead);
        Assert.Equal(3, steps[^1].Counters.RowsOutput);
    }

    [Fact]
    public void Totals_Only_Ever_Increase()
    {
        var steps = Execute(TestIndexPage.Create(1, 2, 3, 4, 5), new IndexPageWalk());

        for (var index = 1; index < steps.Count; index++)
        {
            Assert.True(steps[index].Counters.RowsRead >= steps[index - 1].Counters.RowsRead);
            Assert.True(steps[index].Counters.PagesRead >= steps[index - 1].Counters.PagesRead);
        }
    }

    [Fact]
    public void No_Work_Is_Done_Before_Enumeration()
    {
        var page = TestIndexPage.Create(10, 20, 30);

        var steps = IndexSeekExecutor.Execute(page, new IndexPageWalk { Bounds = SeekBounds.Equality(TestKey.Of(20)) });

        Assert.Equal(0, page.CompareCount);

        steps.ToList();

        Assert.True(page.CompareCount > 0);
    }

    [Fact]
    public void The_Stopped_Step_Carries_The_Highest_Totals()
    {
        var steps = Execute(TestIndexPage.Create(1, 2, 3), new IndexPageWalk());

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(steps.Max(s => s.Counters.RowsRead), stopped.Counters.RowsRead);
        Assert.Equal(steps.Max(s => s.Counters.PagesRead), stopped.Counters.PagesRead);
    }

    [Fact]
    public void Starting_Counters_Are_Carried_Forward()
    {
        var starting = default(AccessCounters).AddPageRead().AddPageRead();

        var steps = Execute(TestIndexPage.Create(1), new IndexPageWalk { Counters = starting });

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(3, stopped.Counters.PagesRead);
    }

    [Fact]
    public void Ghost_Rows_Are_Counted_Separately_From_Rows_Read()
    {
        var page = new TestIndexPage(new(1, 100), [1, 2, 3, 4], new HashSet<int> { 1, 2 });

        var steps = Execute(page, new IndexPageWalk());

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(2, stopped.Counters.GhostsSkipped);
        Assert.Equal(2, stopped.Counters.RowsRead);
    }

    [Fact]
    public void Row_Goal_Stops_The_Scan_Early()
    {
        var steps = Execute(TestIndexPage.Create(1, 2, 3, 4, 5), new IndexPageWalk { RowGoal = 2 });

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(StopReason.RowGoalMet, stopped.Reason);
        Assert.Equal(2, stopped.Counters.RowsOutput);
        Assert.Equal(2, stopped.Counters.RowsRead);
    }

    [Fact]
    public void Seek_Comparisons_Match_The_Work_The_Page_Was_Asked_To_Do()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50, 60, 70, 80);

        var steps = Execute(page, new IndexPageWalk { Bounds = SeekBounds.Equality(TestKey.Of(30)) });

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(page.CompareCount, stopped.Counters.Comparisons);
    }

    [Fact]
    public void Seek_Stops_When_The_Range_Ends()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50);

        var steps = Execute(page, new IndexPageWalk { Bounds = SeekBounds.Between(TestKey.Of(20), TestKey.Of(30)) });

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(StopReason.RangeEnded, stopped.Reason);
        Assert.Equal(2, stopped.Counters.RowsOutput);
    }

    private static List<AccessStep> Execute(TestIndexPage page, IndexPageWalk walk)
    {
        return [.. IndexSeekExecutor.Execute(page, walk)];
    }
}
