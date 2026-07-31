using InternalsViewer.Execution.AccessPaths.Results;

namespace InternalsViewer.Execution.Tests.UnitTests.AccessPaths.Results;

public class AccessCountersTests
{
    [Fact]
    public void Default_Counters_Are_Zero()
    {
        var counters = default(AccessCounters);

        Assert.Equal(0, counters.PagesRead);
        Assert.Equal(0, counters.Comparisons);
        Assert.Equal(0, counters.RowsRead);
        Assert.Equal(0, counters.RowsOutput);
        Assert.Equal(0, counters.GhostsSkipped);
        Assert.Equal(0, counters.LeafLinksFollowed);
    }

    [Fact]
    public void Add_Returns_New_Value_Leaving_Original_Unchanged()
    {
        var original = default(AccessCounters);

        var updated = original.AddPageRead().AddRowRead().AddRowOutput();

        Assert.Equal(0, original.PagesRead);
        Assert.Equal(0, original.RowsRead);
        Assert.Equal(0, original.RowsOutput);

        Assert.Equal(1, updated.PagesRead);
        Assert.Equal(1, updated.RowsRead);
        Assert.Equal(1, updated.RowsOutput);
    }

    [Fact]
    public void Add_Only_Changes_The_Targeted_Total()
    {
        var counters = default(AccessCounters).AddGhostSkipped();

        Assert.Equal(1, counters.GhostsSkipped);
        Assert.Equal(0, counters.RowsRead);
        Assert.Equal(0, counters.PagesRead);
    }

    [Fact]
    public void Add_Comparisons_Accumulates()
    {
        var counters = default(AccessCounters).AddComparisons(3).AddComparisons(2);

        Assert.Equal(5, counters.Comparisons);
    }

    [Fact]
    public void Counters_With_Equal_Totals_Are_Equal()
    {
        var first = default(AccessCounters).AddPageRead().AddRowRead();
        var second = default(AccessCounters).AddRowRead().AddPageRead();

        Assert.Equal(first, second);
    }
}
