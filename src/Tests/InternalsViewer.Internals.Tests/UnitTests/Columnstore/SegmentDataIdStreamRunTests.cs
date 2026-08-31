using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Tests.UnitTests.Columnstore;

[Trait("Category", "Unit")]
[Trait("Area", "Columnstore")]
public class SegmentDataIdStreamRunTests
{
    [Theory]
    [InlineData(0, 900)]
    [InlineData(500, 900)]
    [InlineData(99_800, 900)]
    [InlineData(0, 1)]
    [InlineData(99_999, 64)]
    public void A_Run_Never_Reaches_Past_The_Window(int fromRow, int count)
    {
        var stream = Stream(new RleEntry(7, 100_000));

        AssertWithinWindow(stream.GetRuns(fromRow, count), fromRow, count);
    }

    [Fact]
    public void A_Run_Longer_Than_The_Window_Is_Clipped_To_It()
    {
        var stream = Stream(new RleEntry(7, 100_000));

        var runs = stream.GetRuns(0, 900).ToList();

        Assert.Single(runs);

        Assert.Equal(0, runs[0].FirstRow);

        Assert.Equal(900, runs[0].RowCount);
    }

    [Fact]
    public void A_Window_Past_The_End_Is_Clipped_To_The_Rows_That_Exist()
    {
        var stream = Stream(new RleEntry(7, 100_000));

        var runs = stream.GetRuns(99_800, 900).ToList();

        Assert.Single(runs);

        Assert.Equal(200, runs[0].RowCount);
    }

    [Fact]
    public void A_Window_Spanning_Entries_Clips_Both_Ends()
    {
        var stream = Stream(new RleEntry(7, 500), new RleEntry(-1, 500), new RleEntry(9, 500));

        var runs = stream.GetRuns(400, 700).ToList();

        Assert.Equal(3, runs.Count);

        Assert.Equal([100, 500, 100], runs.Select(r => r.RowCount));

        AssertWithinWindow(runs, 400, 700);
    }

    [Fact]
    public void Every_Row_In_The_Window_Is_Covered_Once()
    {
        var stream = Stream(new RleEntry(7, 500), new RleEntry(-1, 500), new RleEntry(9, 500));

        var runs = stream.GetRuns(400, 700).ToList();

        Assert.Equal(400, runs[0].FirstRow);

        for (var i = 1; i < runs.Count; i++)
        {
            Assert.Equal(runs[i - 1].FirstRow + runs[i - 1].RowCount, runs[i].FirstRow);
        }

        Assert.Equal(1100, runs[^1].FirstRow + runs[^1].RowCount);
    }

    private static void AssertWithinWindow(IEnumerable<SegmentRun> runs, int fromRow, int count)
    {
        foreach (var run in runs)
        {
            Assert.True(run.FirstRow >= fromRow, $"run starts at {run.FirstRow}, before {fromRow}");

            Assert.True(run.FirstRow + run.RowCount <= fromRow + count,
                        $"run ends at {run.FirstRow + run.RowCount}, past {fromRow + count}");
        }
    }

    private static SegmentDataIdStream Stream(params RleEntry[] entries)
        => new(new SegmentBlob { RleEntries = entries });
}
