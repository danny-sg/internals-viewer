using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;

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
    public void A_Read_Overrunning_Its_Bucket_Pushes_The_Next_Read_Clear_Of_It()
    {
        // read1 runs 1068us from bucket 251000, ending at 252068 — past read2's 252000 bucket. read2 must be moved
        // clear of read1's end, not left overlapping it.
        var read1 = Read(timeUs: 251_000, durationUs: 1_068);

        var read2 = Read(timeUs: 252_000, durationUs: 115);

        EventReader.SpreadEvents([read1, read2]);

        Assert.Equal(251_000, read1.TimeUs);
        Assert.True(read2.TimeUs > read1.TimeUs + read1.DurationUs,
            $"read2 ({read2.TimeUs}) must clear read1 end ({read1.TimeUs + read1.DurationUs})");
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

    private static NonCachedReadEventGroup Read(long timeUs, long durationUs) => new()
    {
        Name = "page_read",
        Events = [],
        TimeUs = timeUs,
        DurationUs = durationUs,
        TaskAddress = 1,
    };
}
