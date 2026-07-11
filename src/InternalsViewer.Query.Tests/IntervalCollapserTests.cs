using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Latches;

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
