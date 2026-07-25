using InternalsViewer.Connection.BackupFile.Mapping;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Connection.BackupFile.Tests;

public class PageMapBuilderTests
{
    private const int PageSize = 8192;

    [Fact]
    public void Consecutive_Pages_Collapse_Into_A_Single_Run()
    {
        var builder = new PageMapBuilder();

        builder.AddPage(1, 0, 0, 1000);
        builder.AddPage(1, 1, 0, 1000 + PageSize);
        builder.AddPage(1, 2, 0, 1000 + 2 * PageSize);

        var map = builder.Build();

        var run = Assert.Single(map.Runs[1]);

        Assert.Equal(0, run.StartPageId);
        Assert.Equal(3, run.PageCount);
        Assert.Equal(1000, run.StartOffset);
    }

    [Fact]
    public void Page_Id_Gap_Starts_A_New_Run()
    {
        var builder = new PageMapBuilder();

        builder.AddPage(1, 0, 0, 1000);
        builder.AddPage(1, 1, 0, 1000 + PageSize);
        builder.AddPage(1, 64, 0, 1000 + 2 * PageSize);

        var map = builder.Build();

        Assert.Equal(2, map.Runs[1].Count);

        Assert.True(map.TryGetLocation(new PageAddress(1, 64), out var location));
        Assert.Equal(1000 + 2 * PageSize, location.Offset);

        Assert.False(map.TryGetLocation(new PageAddress(1, 2), out _));
    }

    [Fact]
    public void Stripe_Change_Starts_A_New_Run()
    {
        var builder = new PageMapBuilder();

        builder.AddPage(1, 0, 0, 1000);
        builder.AddPage(1, 1, 1, 1000 + PageSize);

        var map = builder.Build();

        Assert.Equal(2, map.Runs[1].Count);

        Assert.True(map.TryGetLocation(new PageAddress(1, 0), out var firstLocation));
        Assert.Equal(0, firstLocation.StripeIndex);

        Assert.True(map.TryGetLocation(new PageAddress(1, 1), out var secondLocation));
        Assert.Equal(1, secondLocation.StripeIndex);
        Assert.Equal(1000 + PageSize, secondLocation.Offset);
    }

    [Fact]
    public void Unidentified_Page_Extends_The_Active_Run()
    {
        var builder = new PageMapBuilder();

        builder.AddPage(1, 0, 0, 1000);

        Assert.True(builder.TryAddUnidentifiedPage(0, 1000 + PageSize));

        builder.AddPage(1, 2, 0, 1000 + 2 * PageSize);

        var map = builder.Build();

        var run = Assert.Single(map.Runs[1]);

        Assert.Equal(3, run.PageCount);

        Assert.True(map.TryGetLocation(new PageAddress(1, 1), out var location));
        Assert.Equal(1000 + PageSize, location.Offset);
    }

    [Fact]
    public void Unidentified_Page_Without_An_Active_Run_Is_Ignored()
    {
        var builder = new PageMapBuilder();

        Assert.False(builder.TryAddUnidentifiedPage(0, 1000));

        var map = builder.Build();

        Assert.Empty(map.Runs);
    }

    [Fact]
    public void Later_Run_Overrides_Earlier_Run_For_Overlapping_Pages()
    {
        var builder = new PageMapBuilder();

        for (var pageId = 0; pageId < 10; pageId++)
        {
            builder.AddPage(1, pageId, 0, 1000 + (long)pageId * PageSize);
        }

        builder.CloseRun();

        builder.AddPage(1, 0, 0, 500000);
        builder.AddPage(1, 1, 0, 500000 + PageSize);

        var map = builder.Build();

        Assert.True(map.TryGetLocation(new PageAddress(1, 1), out var overriddenLocation));
        Assert.Equal(500000 + PageSize, overriddenLocation.Offset);

        Assert.True(map.TryGetLocation(new PageAddress(1, 5), out var originalLocation));
        Assert.Equal(1000 + 5 * PageSize, originalLocation.Offset);
    }

    [Fact]
    public void Files_Are_Tracked_Separately()
    {
        var builder = new PageMapBuilder();

        builder.AddPage(1, 10, 0, 1000);
        builder.AddPage(3, 10, 0, 1000 + PageSize);

        var map = builder.Build();

        Assert.Equal(2, map.Runs[1].Count + map.Runs[3].Count);

        Assert.True(map.TryGetLocation(new PageAddress(1, 10), out var file1Location));
        Assert.Equal(1000, file1Location.Offset);

        Assert.True(map.TryGetLocation(new PageAddress(3, 10), out var file3Location));
        Assert.Equal(1000 + PageSize, file3Location.Offset);

        Assert.False(map.TryGetLocation(new PageAddress(2, 10), out _));
    }
}
