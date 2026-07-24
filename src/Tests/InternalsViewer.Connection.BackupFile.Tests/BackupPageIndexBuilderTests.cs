using InternalsViewer.Connection.BackupFile.Index;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Connection.BackupFile.Tests;

public class BackupPageIndexBuilderTests
{
    private const int PageSize = 8192;

    [Fact]
    public void Consecutive_Pages_Collapse_Into_A_Single_Run()
    {
        var builder = new BackupPageIndexBuilder();

        builder.AddPage(1, 0, 1000);
        builder.AddPage(1, 1, 1000 + PageSize);
        builder.AddPage(1, 2, 1000 + 2 * PageSize);

        var index = builder.Build();

        var run = Assert.Single(index.Runs[1]);

        Assert.Equal(0, run.StartPageId);
        Assert.Equal(3, run.PageCount);
        Assert.Equal(1000, run.StartOffset);
    }

    [Fact]
    public void Page_Id_Gap_Starts_A_New_Run()
    {
        var builder = new BackupPageIndexBuilder();

        builder.AddPage(1, 0, 1000);
        builder.AddPage(1, 1, 1000 + PageSize);
        builder.AddPage(1, 64, 1000 + 2 * PageSize);

        var index = builder.Build();

        Assert.Equal(2, index.Runs[1].Count);

        Assert.True(index.TryGetOffset(new PageAddress(1, 64), out var offset));
        Assert.Equal(1000 + 2 * PageSize, offset);

        Assert.False(index.TryGetOffset(new PageAddress(1, 2), out _));
    }

    [Fact]
    public void Unidentified_Page_Extends_The_Active_Run()
    {
        var builder = new BackupPageIndexBuilder();

        builder.AddPage(1, 0, 1000);

        Assert.True(builder.TryAddUnidentifiedPage(1000 + PageSize));

        builder.AddPage(1, 2, 1000 + 2 * PageSize);

        var index = builder.Build();

        var run = Assert.Single(index.Runs[1]);

        Assert.Equal(3, run.PageCount);

        Assert.True(index.TryGetOffset(new PageAddress(1, 1), out var offset));
        Assert.Equal(1000 + PageSize, offset);
    }

    [Fact]
    public void Unidentified_Page_Without_An_Active_Run_Is_Ignored()
    {
        var builder = new BackupPageIndexBuilder();

        Assert.False(builder.TryAddUnidentifiedPage(1000));

        var index = builder.Build();

        Assert.Empty(index.Runs);
    }

    [Fact]
    public void Later_Run_Overrides_Earlier_Run_For_Overlapping_Pages()
    {
        var builder = new BackupPageIndexBuilder();

        for (var pageId = 0; pageId < 10; pageId++)
        {
            builder.AddPage(1, pageId, 1000 + (long)pageId * PageSize);
        }

        builder.CloseRun();

        builder.AddPage(1, 0, 500000);
        builder.AddPage(1, 1, 500000 + PageSize);

        var index = builder.Build();

        Assert.True(index.TryGetOffset(new PageAddress(1, 1), out var overriddenOffset));
        Assert.Equal(500000 + PageSize, overriddenOffset);

        Assert.True(index.TryGetOffset(new PageAddress(1, 5), out var originalOffset));
        Assert.Equal(1000 + 5 * PageSize, originalOffset);
    }

    [Fact]
    public void Files_Are_Tracked_Separately()
    {
        var builder = new BackupPageIndexBuilder();

        builder.AddPage(1, 10, 1000);
        builder.AddPage(3, 10, 1000 + PageSize);

        var index = builder.Build();

        Assert.Equal(2, index.Runs[1].Count + index.Runs[3].Count);

        Assert.True(index.TryGetOffset(new PageAddress(1, 10), out var file1Offset));
        Assert.Equal(1000, file1Offset);

        Assert.True(index.TryGetOffset(new PageAddress(3, 10), out var file3Offset));
        Assert.Equal(1000 + PageSize, file3Offset);

        Assert.False(index.TryGetOffset(new PageAddress(2, 10), out _));
    }
}
