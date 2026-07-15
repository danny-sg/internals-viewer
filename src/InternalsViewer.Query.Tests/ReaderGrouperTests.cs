using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.Tests;

public class ReaderGrouperTests
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

        var result = ReaderGrouper.Group([suspend, read]);

        var group = Assert.IsType<ReadEventGroup>(Assert.Single(result));

        Assert.Equal(ReadType.NonCached, group.ReadType);
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

        var result = ReaderGrouper.Group([acquire]);

        var group = Assert.IsType<ReadEventGroup>(Assert.Single(result));

        Assert.Equal(ReadType.Cached, group.ReadType);
        Assert.Equal(1_000, group.TimeUs);
        Assert.Equal(50, group.DurationUs);
    }

    [Fact]
    public void Fully_Cached_Query_With_No_Suspends_Still_Produces_Read_Groups()
    {
        var first = BufferLatch("latch_acquired", page: new PageAddress(1, 100), timeUs: 1_000, durationUs: 40);

        var second = BufferLatch("latch_acquired", page: new PageAddress(1, 200), timeUs: 2_000, durationUs: 60);

        var result = ReaderGrouper.Group([first, second]);

        Assert.Equal(2, result.OfType<ReadEventGroup>().Count());
        Assert.All(result.OfType<ReadEventGroup>(), g => Assert.Equal(ReadType.Cached, g.ReadType));
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

        var result = ReaderGrouper.Group([gather, readA, readB, load, outsideRange]);

        var group = result.OfType<ReadEventGroup>().Single();

        Assert.Equal(ReadType.NonCached, group.ReadType);
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

        var result = ReaderGrouper.Group([file, read]);

        var group = result.OfType<ReadEventGroup>().Single();

        Assert.Equal(ReadType.NonCached, group.ReadType);
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

        var result = ReaderGrouper.Group([orphan]);

        Assert.Empty(result);
    }

    [Fact]
    public void Trailing_Buffer_Latch_On_The_Just_Loaded_Page_Folds_Into_The_Non_Cached_Read()
    {
        // The worker resumes a scheduling quantum after the load and reads the page it just brought in - a bare SH
        // acquire on the SAME page and buffer-latch address, ~10ms later. It is the tail of that read, not a second one.
        var suspend = Suspend(latchAddress: 200, page: new PageAddress(1, 1_784), timeUs: 2_000, durationUs: 72);

        var read = new IoEvent
        {
            Name = "physical_page_read",
            IsRead = true,
            PageAddress = new PageAddress(1, 1_784),
            TimeUs = 2_000,
        };

        var trailingRead = BufferLatch("latch_acquired", latchAddress: 200, page: new PageAddress(1, 1_784), timeUs: 12_000, durationUs: 0);

        var result = ReaderGrouper.Group([suspend, read, trailingRead]);

        var group = Assert.IsType<ReadEventGroup>(Assert.Single(result));

        Assert.Equal(ReadType.NonCached, group.ReadType);
        Assert.Contains(trailingRead, group.Events);
    }

    [Fact]
    public void Buffer_Latch_Long_After_The_Load_Stays_A_Separate_Cached_Read()
    {
        // A re-read of the (still resident) page much later in the query is a genuine cached read, not the load's tail.
        var suspend = Suspend(latchAddress: 200, page: new PageAddress(1, 1_784), timeUs: 2_000, durationUs: 72);

        var read = new IoEvent { Name = "physical_page_read", IsRead = true, PageAddress = new PageAddress(1, 1_784), TimeUs = 2_000 };

        var later = BufferLatch("latch_acquired", latchAddress: 200, page: new PageAddress(1, 1_784), timeUs: 30_000, durationUs: 40);

        var result = ReaderGrouper.Group([suspend, read, later]);

        Assert.Equal(2, result.OfType<ReadEventGroup>().Count());

        var cached = result.OfType<ReadEventGroup>().Single(g => g.ReadType == ReadType.Cached);

        Assert.Contains(later, cached.Events);
    }

    [Fact]
    public void Buffer_Latch_On_A_Recycled_Frame_Does_Not_Fold_Into_The_Prior_Read()
    {
        // Same buffer-latch address but a DIFFERENT page: the frame was reused for another page, so the SH acquire is
        // its own read and must not be pulled into the earlier page's group.
        var suspend = Suspend(latchAddress: 200, page: new PageAddress(1, 1_784), timeUs: 2_000, durationUs: 72);

        var read = new IoEvent { Name = "physical_page_read", IsRead = true, PageAddress = new PageAddress(1, 1_784), TimeUs = 2_000 };

        var recycled = BufferLatch("latch_acquired", latchAddress: 200, page: new PageAddress(1, 9_999), timeUs: 4_000, durationUs: 40);

        var result = ReaderGrouper.Group([suspend, read, recycled]);

        var cached = result.OfType<ReadEventGroup>().Single(g => g.ReadType == ReadType.Cached);

        Assert.Contains(recycled, cached.Events);
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

    private static LatchEvent BufferLatch(string name, PageAddress page, long timeUs, long durationUs, ulong latchAddress = 0) => new()
    {
        Name = name,
        LatchClass = LatchClass.BUF,
        LatchMode = LatchMode.SH,
        LatchAddress = latchAddress == 0 ? null : latchAddress,
        PageAddress = page,
        TimeUs = timeUs,
        DurationUs = durationUs,
    };
}
