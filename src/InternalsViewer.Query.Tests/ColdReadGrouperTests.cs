using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Locks;

namespace InternalsViewer.Query.Tests;

public class ColdReadGrouperTests
{
    [Fact]
    public void Suspend_With_Physical_Read_Becomes_A_Non_Cached_Group_Timed_From_The_Spine()
    {
        var suspend = Suspend(latchAddress: 200, page: new PageAddress(1, 464), timeUs: 2_000, durationUs: 922);

        var read = new IoEvent
        {
            Name = "physical_page_read",
            IsRead = true,
            PageAddress = new PageAddress(1, 464),
            TimeUs = 2_000,
        };

        var result = ColdReadGrouper.Group([suspend, read]);

        var group = Assert.IsType<NonCachedReadEventGroup>(Assert.Single(result));

        Assert.Equal(ReadKind.NonCached, group.Kind);
        Assert.Equal(2_000, group.TimeUs);
        Assert.Equal(922, group.DurationUs);
        Assert.Contains(read, group.Events);
    }

    [Fact]
    public void Bare_Buffer_Latch_Acquire_With_No_Suspend_Becomes_A_Cached_Group()
    {
        // The release has already been folded into the acquire upstream (IntervalCollapser), so its DurationUs is the
        // hold time.
        var acquire = BufferLatch("latch_acquired", page: new PageAddress(1, 472), timeUs: 1_000, durationUs: 50);

        var result = ColdReadGrouper.Group([acquire]);

        var group = Assert.IsType<NonCachedReadEventGroup>(Assert.Single(result));

        Assert.Equal(ReadKind.Cached, group.Kind);
        Assert.Equal(1_000, group.TimeUs);
        Assert.Equal(50, group.DurationUs);
    }

    [Fact]
    public void Fully_Cached_Query_With_No_Suspends_Still_Produces_Read_Groups()
    {
        var first = BufferLatch("latch_acquired", page: new PageAddress(1, 100), timeUs: 1_000, durationUs: 40);

        var second = BufferLatch("latch_acquired", page: new PageAddress(1, 200), timeUs: 2_000, durationUs: 60);

        var result = ColdReadGrouper.Group([first, second]);

        Assert.Equal(2, result.OfType<NonCachedReadEventGroup>().Count());
        Assert.All(result.OfType<NonCachedReadEventGroup>(), g => Assert.Equal(ReadKind.Cached, g.Kind));
    }

    [Fact]
    public void Scatter_Gather_File_Read_Becomes_One_Multi_Page_Non_Cached_Group()
    {
        // A gather read of 4 pages from 1000 (Size 32768 = 4 * 8192). The physical reads and the EX load latches for
        // pages in its range attach to it, forming one multi-page Non-Cached group.
        var gather = new FileEvent
        {
            Name = "file_read_completed",
            Mode = ReadMode.ScatterGather,
            FileId = 1,
            Offset = 1_000 * 8_192,
            Size = 4 * 8_192,
            PageAddress = new PageAddress(1, 1_000),
            IsRead = true,
            TimeUs = 5_000,
        };

        var readA = new IoEvent { Name = "physical_page_read", IsRead = true, PageAddress = new PageAddress(1, 1_001), TimeUs = 5_000 };

        var readB = new IoEvent { Name = "physical_page_read", IsRead = true, PageAddress = new PageAddress(1, 1_003), TimeUs = 5_000 };

        var load = new LatchEvent { Name = "latch_acquired", LatchClass = LatchClass.BUF, LatchMode = LatchMode.EX, PageAddress = new PageAddress(1, 1_002), TimeUs = 5_000 };

        var outsideRange = new IoEvent { Name = "physical_page_read", IsRead = true, PageAddress = new PageAddress(1, 2_000), TimeUs = 5_000 };

        var result = ColdReadGrouper.Group([gather, readA, readB, load, outsideRange]);

        var group = result.OfType<NonCachedReadEventGroup>().Single();

        Assert.Equal(ReadKind.NonCached, group.Kind);
        Assert.Equal(4, group.PageCount);
        Assert.Contains(readA, group.Events);
        Assert.Contains(readB, group.Events);
        Assert.Contains(load, group.Events);

        // The read on page 2000 is outside the gather's 1000-1003 range, so it stays a bare event.
        Assert.Contains(outsideRange, result);
    }

    [Fact]
    public void Contiguous_File_Read_Without_A_Suspend_Becomes_A_Single_Page_Non_Cached_Group()
    {
        // A cold single-page read that finished without the worker suspending (fast I/O): no suspend spine, but the
        // file read + physical read still make it a non-cached read.
        var file = new FileEvent
        {
            Name = "file_read",
            Mode = ReadMode.Contiguous,
            FileId = 1,
            Offset = 500 * 8_192,
            Size = 8_192,
            PageAddress = new PageAddress(1, 500),
            IsRead = true,
            TimeUs = 3_000,
        };

        var read = new IoEvent { Name = "physical_page_read", IsRead = true, PageAddress = new PageAddress(1, 500), TimeUs = 3_000 };

        var result = ColdReadGrouper.Group([file, read]);

        var group = result.OfType<NonCachedReadEventGroup>().Single();

        Assert.Equal(ReadKind.NonCached, group.Kind);
        Assert.Equal(1, group.PageCount);
        Assert.Contains(read, group.Events);
    }

    [Fact]
    public void Incomplete_File_Read_With_Zero_Size_Is_Dropped()
    {
        // A file read whose completed never folded in (Size 0) is a cancelled I/O that moved no pages — dropped.
        var orphan = new FileEvent
        {
            Name = "file_read",
            Mode = ReadMode.ScatterGather,
            FileId = 1,
            Offset = 500 * 8_192,
            Size = 0,
            PageAddress = new PageAddress(1, 500),
            IsRead = true,
            TimeUs = 3_000,
        };

        var result = ColdReadGrouper.Group([orphan]);

        Assert.Empty(result);
    }

    private static LatchEvent Suspend(ulong latchAddress, PageAddress page, long timeUs, long durationUs) => new()
    {
        Name = "latch_suspend_begin",
        LatchClass = LatchClass.BUF,
        LatchMode = LatchMode.SH,
        LatchAddress = latchAddress,
        PageAddress = page,
        TimeUs = timeUs,
        DurationUs = durationUs,
    };

    private static LatchEvent BufferLatch(string name, PageAddress page, long timeUs, long durationUs) => new()
    {
        Name = name,
        LatchClass = LatchClass.BUF,
        LatchMode = LatchMode.SH,
        PageAddress = page,
        TimeUs = timeUs,
        DurationUs = durationUs,
    };
}
