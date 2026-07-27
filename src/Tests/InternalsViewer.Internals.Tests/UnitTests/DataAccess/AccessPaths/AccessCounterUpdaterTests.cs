using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.Executors;

namespace InternalsViewer.Internals.Tests.UnitTests.DataAccess.AccessPaths;

public class AccessCounterUpdaterTests
{
    [Fact]
    public void Updater_Receives_Every_Change()
    {
        var updates = new List<AccessCounters>();

        var executor = new PageScanExecutor(new TestRowBinder());

        executor.Execute(TestIndexPage.Create(1, 2, 3),
                         onCountersChanged: updates.Add)
                .ToList();

        Assert.Equal(1, updates[0].PagesRead);

        Assert.Equal(3, updates[^1].RowsRead);
        Assert.Equal(3, updates[^1].RowsOutput);
    }

    [Fact]
    public void Updater_Totals_Only_Ever_Increase()
    {
        var updates = new List<AccessCounters>();

        var executor = new PageScanExecutor(new TestRowBinder());

        executor.Execute(TestIndexPage.Create(1, 2, 3, 4, 5),
                         onCountersChanged: updates.Add)
                .ToList();

        for (var index = 1; index < updates.Count; index++)
        {
            Assert.True(updates[index].RowsRead >= updates[index - 1].RowsRead);
            Assert.True(updates[index].PagesRead >= updates[index - 1].PagesRead);
        }
    }

    [Fact]
    public void Updater_Is_Not_Called_Before_Enumeration()
    {
        var called = false;

        var executor = new PageScanExecutor(new TestRowBinder());

        var steps = executor.Execute(TestIndexPage.Create(1, 2),
                                     onCountersChanged: _ => called = true);

        Assert.False(called);

        steps.ToList();

        Assert.True(called);
    }

    [Fact]
    public void Final_Update_Matches_The_Stopped_Step()
    {
        var updates = new List<AccessCounters>();

        var executor = new PageScanExecutor(new TestRowBinder());

        var steps = executor.Execute(TestIndexPage.Create(1, 2, 3),
                                     onCountersChanged: updates.Add)
                            .ToList();

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(updates[^1], stopped.Counters);
    }

    [Fact]
    public void Starting_Counters_Are_Carried_Forward()
    {
        var starting = default(AccessCounters).AddPageRead().AddPageRead();

        var executor = new PageScanExecutor(new TestRowBinder());

        var steps = executor.Execute(TestIndexPage.Create(1), counters: starting).ToList();

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(3, stopped.Counters.PagesRead);
    }

    [Fact]
    public void Ghost_Rows_Are_Counted_Separately_From_Rows_Read()
    {
        var page = new TestIndexPage(new(1, 100), [1, 2, 3, 4], new HashSet<int> { 1, 2 });

        var executor = new PageScanExecutor(new TestRowBinder());

        var steps = executor.Execute(page).ToList();

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(2, stopped.Counters.GhostsSkipped);
        Assert.Equal(2, stopped.Counters.RowsRead);
    }

    [Fact]
    public void Row_Goal_Stops_The_Scan_Early()
    {
        var executor = new PageScanExecutor(new TestRowBinder());

        var steps = executor.Execute(TestIndexPage.Create(1, 2, 3, 4, 5), rowGoal: 2).ToList();

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(StopReason.RowGoalMet, stopped.Reason);
        Assert.Equal(2, stopped.Counters.RowsOutput);
        Assert.Equal(2, stopped.Counters.RowsRead);
    }

    [Fact]
    public void Seek_Comparisons_Match_The_Work_The_Page_Was_Asked_To_Do()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50, 60, 70, 80);

        var executor = new PageSeekExecutor(new TestRowBinder());

        var steps = executor.Execute(page, SeekBounds.Equality(TestKey.Of(30)), ScanDirection.Forward)
                            .ToList();

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(page.CompareCount, stopped.Counters.Comparisons);
    }

    [Fact]
    public void Seek_Stops_When_The_Range_Ends()
    {
        var page = TestIndexPage.Create(10, 20, 30, 40, 50);

        var executor = new PageSeekExecutor(new TestRowBinder());

        var steps = executor.Execute(page,
                                     SeekBounds.Between(TestKey.Of(20), TestKey.Of(30)),
                                     ScanDirection.Forward)
                            .ToList();

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(StopReason.RangeEnded, stopped.Reason);
        Assert.Equal(2, stopped.Counters.RowsOutput);
    }
}
