using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.Tests;

public class IntervalCollapserTests
{
    [Fact]
    public void Latch_Release_Is_Folded_Into_Its_Acquire_Even_When_Captured_First()
    {
        // Capture order is unreliable: the release is buffered ahead of its acquire. The fold must still pair them by
        // latch address and move the release's hold duration onto the acquire.
        var release = Latch("latch_released", latchAddress: 500, timeUs: 1_000, durationUs: 75);

        var acquire = Latch("latch_acquired", latchAddress: 500, timeUs: 1_000, durationUs: 0);

        var result = IntervalCollapser.Collapse([release, acquire]);

        var kept = Assert.IsType<LatchEvent>(Assert.Single(result));

        Assert.Equal("latch_acquired", kept.Name);
        Assert.Equal(75, kept.DurationUs);
    }

    [Fact]
    public void Repeated_Holds_On_One_Latch_Pair_Positionally_In_Time_Order()
    {
        var acquire1 = Latch("latch_acquired", latchAddress: 500, timeUs: 1_000, durationUs: 0);

        var release1 = Latch("latch_released", latchAddress: 500, timeUs: 1_000, durationUs: 10);

        var acquire2 = Latch("latch_acquired", latchAddress: 500, timeUs: 2_000, durationUs: 0);

        var release2 = Latch("latch_released", latchAddress: 500, timeUs: 2_000, durationUs: 20);

        var result = IntervalCollapser.Collapse([acquire1, release1, acquire2, release2]);

        var acquires = result.OfType<LatchEvent>().Where(l => l.Name == "latch_acquired").OrderBy(l => l.TimeUs).ToList();

        Assert.Equal(2, acquires.Count);
        Assert.Equal(10, acquires[0].DurationUs);
        Assert.Equal(20, acquires[1].DurationUs);
    }

    [Fact]
    public void File_Read_Completed_Folds_Its_Size_Onto_The_Begin()
    {
        // Only the completed carries the size (the page range); the begin (offset only) must take it, and the completed
        // is dropped. They pair by offset.
        var begin = new FileEvent { Name = "file_read", IsRead = true, Offset = 8_192, Size = 0, TimeUs = 1_000 };

        var completed = new FileEvent { Name = "file_read_completed", IsRead = true, Offset = 8_192, Size = 4 * 8_192, TimeUs = 1_000 };

        var result = IntervalCollapser.Collapse([begin, completed]);

        var kept = Assert.IsType<FileEvent>(Assert.Single(result));

        Assert.Equal("file_read", kept.Name);
        Assert.Equal(4 * 8_192, kept.Size);
    }

    [Fact]
    public void Lock_Release_Is_Folded_Into_Its_Acquire_With_The_Elapsed_Hold()
    {
        // Locks carry no measured duration, so the fold gives the acquire the elapsed hold (release time less acquire
        // time) and drops the release. Paired by resource key even though the release was captured first.
        var release = Lock("lock_released", resourceKey: 900, timeUs: 5_000);

        var acquire = Lock("lock_acquired", resourceKey: 900, timeUs: 2_000);

        var result = IntervalCollapser.Collapse([release, acquire]);

        var kept = Assert.IsType<LockEvent>(Assert.Single(result));

        Assert.Equal("Lock", kept.Name);
        Assert.Equal(3_000, kept.DurationUs);
    }

    [Fact]
    public void Repeated_Lock_Holds_On_One_Resource_Pair_In_Time_Order_Regardless_Of_Capture_Order()
    {
        // Two acquire/release cycles on the SAME resource, captured fully out of order (both releases ahead of both
        // acquires). The fold buckets acquires and releases by key separately, sorts each by time and zips positionally,
        // so hold 1 = [1000,1500] and hold 2 = [3000,3800] — never a cross-paired [1000,3800].
        var release1 = Lock("lock_released", resourceKey: 7, timeUs: 1_500);

        var release2 = Lock("lock_released", resourceKey: 7, timeUs: 3_800);

        var acquire1 = Lock("lock_acquired", resourceKey: 7, timeUs: 1_000);

        var acquire2 = Lock("lock_acquired", resourceKey: 7, timeUs: 3_000);

        var result = IntervalCollapser.Collapse([release2, release1, acquire2, acquire1]);

        var holds = result.OfType<LockEvent>().OrderBy(l => l.TimeUs).ToList();

        Assert.Equal(2, holds.Count);
        Assert.All(holds, h => Assert.Equal("Lock", h.Name));
        Assert.Equal(500, holds[0].DurationUs); // 1000 -> 1500
        Assert.Equal(800, holds[1].DurationUs); // 3000 -> 3800
    }

    [Fact]
    public void An_Acquire_With_No_Release_Is_Left_Unpaired_As_An_Open_Hold()
    {
        // The other lock in the batch has both ends; the lone acquire must survive as an open (unfolded) acquire, not
        // steal the unrelated release.
        var acquire = Lock("lock_acquired", resourceKey: 10, timeUs: 1_000);

        var pairedAcquire = Lock("lock_acquired", resourceKey: 20, timeUs: 2_000);

        var pairedRelease = Lock("lock_released", resourceKey: 20, timeUs: 2_500);

        var result = IntervalCollapser.Collapse([pairedRelease, acquire, pairedAcquire]).OfType<LockEvent>().ToList();

        var open = Assert.Single(result, l => l.Resource.Key == 10);

        Assert.Equal("lock_acquired", open.Name);
        Assert.Equal(0, open.DurationUs);

        var folded = Assert.Single(result, l => l.Resource.Key == 20);

        Assert.Equal("Lock", folded.Name);
    }

    [Fact]
    public void Locks_On_Different_Resources_Are_Not_Paired()
    {
        var acquire = Lock("lock_acquired", resourceKey: 1, timeUs: 1_000);

        var release = Lock("lock_released", resourceKey: 2, timeUs: 2_000);

        var result = IntervalCollapser.Collapse([acquire, release]);

        Assert.Equal(2, result.Count);
    }

    private static LockEvent Lock(string name, ulong resourceKey, long timeUs) => new()
    {
        Name = name,
        Resource = new LockResource { Key = resourceKey },
        TimeUs = timeUs,
    };

    [Fact]
    public void The_Folded_End_Is_Owned_By_The_Begin_That_Consumed_It()
    {
        // The End leaves the stream, but it carries its own call stack (the release path) and that work belongs to the
        // Begin that survived. Anything scoping by the surviving events — the crop's call-stack keep set — reaches it
        // through here; without it the End's frames are pruned and the tree loses every release/completion path.
        var acquire = Latch("latch_acquired", latchAddress: 500, timeUs: 1_000, durationUs: 0);

        var release = Latch("latch_released", latchAddress: 500, timeUs: 1_100, durationUs: 75);

        var result = IntervalCollapser.Collapse([acquire, release]);

        var kept = Assert.IsType<LatchEvent>(Assert.Single(result));

        Assert.Same(release, kept.FoldedFrom);
    }

    [Fact]
    public void An_Unpaired_Begin_Owns_Nothing()
    {
        var acquire = Latch("latch_acquired", latchAddress: 500, timeUs: 1_000, durationUs: 0);

        var result = IntervalCollapser.Collapse([acquire]);

        Assert.Null(Assert.IsType<LatchEvent>(Assert.Single(result)).FoldedFrom);
    }

    private static LatchEvent Latch(string name, ulong latchAddress, long timeUs, long durationUs) => new()
    {
        Name = name,
        LatchClass = LatchClass.BUF,
        LatchMode = LatchMode.SH,
        LatchAddress = latchAddress,
        PageAddress = new PageAddress(1, 100),
        TimeUs = timeUs,
        DurationUs = durationUs,
    };
}
