using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.Latches;

namespace InternalsViewer.Query.Tests;

public class BufferLatchCoalescingTests
{
    [Fact]
    public void A_Run_Of_Identical_Buffer_Latches_On_One_Page_Collapses_To_One()
    {
        // A scan re-latches the page per row: many identical BUF SH acquires on one page in the same instant. They are
        // one page visit, so they collapse to a single event whose hold spans the run.
        var latches = new[]
        {
            Latch(page: 100, timeUs: 1_000, durationUs: 5),
            Latch(page: 100, timeUs: 1_000, durationUs: 5),
            Latch(page: 100, timeUs: 1_200, durationUs: 8),
        };

        var result = BufferLatchCoalescing.Coalesce(latches);

        var kept = Assert.IsType<LatchEvent>(Assert.Single(result));

        Assert.Equal(1_000, kept.TimeUs);
        Assert.Equal(208, kept.DurationUs); // extended from first acquire (1_000) to the last release (1_200 + 8)
    }

    [Fact]
    public void Different_Modes_On_The_Same_Page_Stay_Separate()
    {
        // A shared read and a keep latch are different latches — they must not merge into one.
        var shared = Latch(page: 100, timeUs: 1_000, durationUs: 5, mode: LatchMode.SH);

        var keep = Latch(page: 100, timeUs: 1_000, durationUs: 5, mode: LatchMode.KP);

        var result = BufferLatchCoalescing.Coalesce([shared, keep]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Latches_On_Different_Pages_Stay_Separate()
    {
        var result = BufferLatchCoalescing.Coalesce(
        [
            Latch(page: 100, timeUs: 1_000, durationUs: 5),
            Latch(page: 200, timeUs: 1_000, durationUs: 5),
        ]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void A_Later_Re_Read_Of_The_Same_Page_Stays_A_Separate_Event()
    {
        // Two visits to one page far enough apart in time are two reads, not one, so the gap keeps them separate.
        var result = BufferLatchCoalescing.Coalesce(
        [
            Latch(page: 100, timeUs: 1_000, durationUs: 5),
            Latch(page: 100, timeUs: 50_000, durationUs: 5),
        ]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Non_Buffer_Latches_Are_Left_Untouched()
    {
        var hobt1 = Latch(page: 0, timeUs: 1_000, durationUs: 5, latchClass: LatchClass.HOBT);

        var hobt2 = Latch(page: 0, timeUs: 1_000, durationUs: 5, latchClass: LatchClass.HOBT);

        var result = BufferLatchCoalescing.Coalesce([hobt1, hobt2]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Coalesced_Acquires_Stay_Reachable_Through_The_Heads_Fold_Chain()
    {
        // The dropped duplicates leave the stream, but their call stacks (and their own folded releases) are still this
        // page visit's work — the crop's keep set expands through SelfAndOwned, so the head must own them.
        var head = Latch(page: 100, timeUs: 1_000, durationUs: 5);

        var duplicate = Latch(page: 100, timeUs: 1_200, durationUs: 8);

        var release = Latch(page: 100, timeUs: 1_208, durationUs: 8);

        release.Name = "latch_released";

        duplicate.FoldedFrom = release; // as IntervalCollapser leaves a folded hold

        var result = BufferLatchCoalescing.Coalesce([head, duplicate]);

        Assert.Same(head, Assert.Single(result));

        var owned = head.SelfAndOwned().ToList();

        Assert.Contains(duplicate, owned);
        Assert.Contains(release, owned);
    }

    private static LatchEvent Latch(int page,
                                    long timeUs,
                                    long durationUs,
                                    LatchMode mode = LatchMode.SH,
                                    LatchClass latchClass = LatchClass.BUF) => new()
    {
        Name = "latch_acquired",
        LatchClass = latchClass,
        LatchMode = mode,
        PageAddress = new PageAddress(1, page),
        TimeUs = timeUs,
        DurationUs = durationUs,
    };
}
