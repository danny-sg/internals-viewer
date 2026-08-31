using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Waits;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class WaitAlignerTests
{
    [Fact]
    public void Page_Io_Wait_Takes_Its_Window_And_Page_From_The_Suspend()
    {
        // wait_info reports duration in milliseconds, so a sub-millisecond read arrives as a zero-duration wait. Only the
        // suspend measures the pause in microseconds.
        var suspend = Suspend(latchAddress: 200, page: new PageAddress(1, 215), timeUs: 2_000, durationUs: 973);

        var wait = PageIoWait(waitResource: 200, timeUs: 2_004, durationUs: 0);

        WaitAligner.Align([suspend, wait]);

        Assert.Equal(2_000, wait.TimeUs);
        Assert.Equal(973, wait.DurationUs);
        Assert.Equal(new PageAddress(1, 215), wait.PageAddress);
    }

    [Fact]
    public void Wait_With_No_Matching_Suspend_Is_Left_Alone()
    {
        var suspend = Suspend(latchAddress: 200, page: new PageAddress(1, 215), timeUs: 2_000, durationUs: 973);

        var wait = PageIoWait(waitResource: 999, timeUs: 5_000, durationUs: 0);

        WaitAligner.Align([suspend, wait]);

        Assert.Equal(5_000, wait.TimeUs);
        Assert.Null(wait.PageAddress);
    }

    [Fact]
    public void Non_Page_Io_Wait_Is_Left_Alone()
    {
        // A scheduler yield has no latch behind it, and its wait_resource of 0 would otherwise collide with any latch
        // address that happened to be keyed 0.
        var suspend = Suspend(latchAddress: 0, page: new PageAddress(1, 215), timeUs: 2_000, durationUs: 973);

        var wait = new WaitEvent
        {
            Name = "Wait",
            WaitType = WaitType.SOS_SCHEDULER_YIELD,
            WaitResource = 0,
            TimeUs = 26_500,
        };

        WaitAligner.Align([suspend, wait]);

        Assert.Equal(26_500, wait.TimeUs);
        Assert.Null(wait.PageAddress);
    }

    [Fact]
    public void Wait_Aligns_To_The_Nearest_Suspend_When_A_Buffer_Is_Latched_More_Than_Once()
    {
        // The same buffer is suspended on twice over a query, so the address alone identifies the frame, not the pause.
        var first = Suspend(latchAddress: 200, page: new PageAddress(1, 215), timeUs: 2_000, durationUs: 100);

        var second = Suspend(latchAddress: 200, page: new PageAddress(1, 900), timeUs: 50_000, durationUs: 400);

        var wait = PageIoWait(waitResource: 200, timeUs: 49_800, durationUs: 0);

        WaitAligner.Align([first, second, wait]);

        Assert.Equal(50_000, wait.TimeUs);
        Assert.Equal(400, wait.DurationUs);
        Assert.Equal(new PageAddress(1, 900), wait.PageAddress);
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

    private static WaitEvent PageIoWait(ulong waitResource, long timeUs, long durationUs) => new()
    {
        Name = "Wait",
        WaitType = WaitType.PAGEIOLATCH_SH,
        WaitResource = waitResource,
        TimeUs = timeUs,
        DurationUs = durationUs,
    };
}
