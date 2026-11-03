using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Tests.Helpers;

[Trait("Category", "Unit")]
public class HighWordRunArrayTests
{
    [Fact]
    public void Each_Index_Reads_The_High_Word_Of_Its_Run()
    {
        uint[] highs = [0, 0, 2, 2, 0, 3];

        var runs = HighWordRunArray.Build(highs);

        Assert.Equal(highs, Enumerable.Range(0, highs.Length).Select(runs.Get));
    }

    [Fact]
    public void High_Words_That_Are_All_Zero_Need_No_Runs()
    {
        Assert.Same(HighWordRunArray.Empty, HighWordRunArray.Build(new uint[4]));
        Assert.Equal(0u, HighWordRunArray.Empty.Get(3));
    }
}
