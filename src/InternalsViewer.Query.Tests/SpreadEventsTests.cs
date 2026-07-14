using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.Tests;

public class SpreadEventsTests
{
    [Fact]
    public void Reads_Sharing_A_Bucket_Are_Spread_Without_Overlapping_Or_Touching()
    {
        // Three reads captured at the same millisecond (252000us). They must be spread across the bucket window with a
        // gap between each, not stacked and not laid consecutively.
        var reads = new[]
        {
            Read(timeUs: 252_000, durationUs: 200),
            Read(timeUs: 252_000, durationUs: 200),
            Read(timeUs: 252_000, durationUs: 200),
        };

        EventReader.SpreadEvents([.. reads]);

        var ordered = reads.OrderBy(r => r.TimeUs).ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            var gap = ordered[i].TimeUs - (ordered[i - 1].TimeUs + ordered[i - 1].DurationUs);

            Assert.True(gap > 0, $"reads must not overlap or touch; gap was {gap}us");
        }

        // All stay inside the millisecond window they were captured in.
        Assert.All(ordered, r => Assert.InRange(r.TimeUs, 252_000, 253_000));
    }

    [Fact]
    public void A_Reads_Position_Depends_Only_On_Its_Own_Bucket_Not_An_Earlier_Overrun()
    {
        // read1 runs 1068us from bucket 251000, overrunning into read2's 252000 bucket. Under the count-in-window model
        // the layout is confined to each bucket, so read2 is NOT dragged forward by read1 — it stays inside its own
        // millisecond window (which is what stops a dense run from stretching the lane past where it really happened).
        var read1 = Read(timeUs: 251_000, durationUs: 1_068);

        var read2 = Read(timeUs: 252_000, durationUs: 115);

        EventReader.SpreadEvents([read1, read2]);

        Assert.Equal(251_000, read1.TimeUs);
        Assert.InRange(read2.TimeUs, 252_000, 253_000);
    }

    [Fact]
    public void Different_Lanes_Are_Spread_Independently()
    {
        // A read and a latch captured at the same millisecond live in different rows, so they do not compete for space
        // and are each free to sit early in the bucket.
        var read = Read(timeUs: 252_000, durationUs: 100);

        var latch = new LatchEvent { Name = "latch_acquired", TimeUs = 252_000, DurationUs = 100 };

        EventReader.SpreadEvents([read, latch]);

        Assert.InRange(read.TimeUs, 252_000, 253_000);
        Assert.InRange(latch.TimeUs, 252_000, 253_000);
    }

    [Fact]
    public void Members_Move_With_Their_Group_So_None_Precede_It()
    {
        // A grouped read whose members sit at its start and 500us in. When the group is repositioned by the spread
        // (centred in its bucket) its members must move with it by the same delta, not stay at their raw (pre-spread)
        // times — otherwise a member would render before the group it belongs to.
        var member0 = new LatchEvent { Name = "latch_suspend_begin", TimeUs = 252_000, TaskAddress = 1 };

        var member1 = new LatchEvent { Name = "latch_acquired", TimeUs = 252_500, TaskAddress = 1 };

        var group = new ReadEventGroup
        {
            Name = "Page Read",
            Events = [member0, member1],
            TimeUs = 252_000,
            DurationUs = 100,
            TaskAddress = 1,
        };

        EventReader.SpreadEvents([group]);

        // The group is centred in its own bucket, and the members kept their relative offsets but moved by the group's
        // delta, so none start before the group.
        Assert.InRange(group.TimeUs, 252_000, 253_000);
        Assert.Equal(group.TimeUs, member0.TimeUs);
        Assert.Equal(group.TimeUs + 500, member1.TimeUs);
        Assert.All(group.Events, m => Assert.True(m.TimeUs >= group.TimeUs, "no member should precede its group"));
    }

    private static ReadEventGroup Read(long timeUs, long durationUs) => new()
    {
        Name = "Page Read",
        Events = [],
        TimeUs = timeUs,
        DurationUs = durationUs,
        TaskAddress = 1,
    };
}
