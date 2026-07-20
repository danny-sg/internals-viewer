using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Memory;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Transactions;

namespace InternalsViewer.Query.Events;

public static class EventSpreader
{
    // Timestamps resolve only to the millisecond (a true time is within +/-500us of its bucket); durations are the microsecond-accurate
    // signal. Events sharing a bucket are spread across it rather than left stacked.
    private const long BucketUs = 1_000;

    /// <summary>
    /// Spreads millisecond-bucketed events across their own window, per lane, by how many share the window
    /// </summary>
    internal static void SpreadEvents(List<EngineEvent> events)
    {
        foreach (var lane in events.Where(IsSerialWork).GroupBy(LaneOf))
        {
            SpreadLane([.. lane.OrderBy(e => e.TimeUs).ThenBy(e => e.SequenceId)]);
        }
    }

    // Events that render at the same full-row height share one lane.
    private const int NoStep = -1;

    /// <summary>
    /// The lane an event is spread within
    /// </summary>
    /// <remarks>
    /// That is its timeline row (the type) subdivided by however the row stacks its own events — by category for the wait/latch style
    /// rows, by <see cref="ReadType"/> for the split read band. Keying on the type alone over-spreads: events on different steps of a row
    /// never collide, yet each would take a smaller slice of the bucket and be pushed apart for nothing, and the bucket would read busier than the lane really is.
    /// </remarks>
    private static (Type Row, int Step) LaneOf(EngineEvent e) => e switch
    {
        ReadEventGroup group
            => (typeof(ReadEventGroup), (int)group.ReadType),

        _ => (e.GetType(), (int?)e.Category ?? NoStep),
    };

    private static void SpreadLane(List<EngineEvent> lane)
    {
        var i = 0;

        while (i < lane.Count)
        {
            var bucket = lane[i].TimeUs / BucketUs * BucketUs;

            var j = i;

            while (j < lane.Count && lane[j].TimeUs / BucketUs * BucketUs == bucket)
            {
                j++;
            }

            var count = j - i;

            // Density = how many events share this millisecond: each gets an equal 1/count slice of the window.
            var slot = Math.Max(1, BucketUs / count);

            for (var k = 0; k < count; k++)
            {
                var e = lane[i + k];

                // Centre the event in its slice; never let it precede its own bucket. Confined to the bucket — a busy bucket packs tighter
                // (and, when truly over-full, overlaps) instead of overflowing into later ones.
                var slotStart = bucket + k * slot;

                var centred = slotStart + (slot - e.DurationUs) / 2;

                ShiftTo(e, Math.Max(slotStart, centred));
            }

            i = j;
        }
    }

    ///<summary>
    /// Moves an event to its spread start
    /// </summary>
    /// <remarks>
    /// A grouped read carries its members with it (by the same delta) so its child events stay aligned under it. The group's spread
    /// position is the corrected timeline — the members' own timestamps are the ms-quantised ones the spread exists to replace — so
    /// without this the members would show earlier than the group they belong to.
    /// </remarks>
    private static void ShiftTo(EngineEvent e, long start)
    {
        var delta = start - e.TimeUs;

        e.TimeUs = start;

        if (delta != 0 && e is ReadEventGroup group)
        {
            foreach (var member in group.Events)
            {
                member.TimeUs += delta;
            }
        }
    }

    /// <summary>
    /// If the type of event executes in serial
    /// </summary>
    /// <remarks>
    /// Locks (and the escalation / transaction instants that mark their boundaries) are not serial work — they are concurrent holds and
    /// the moments those holds start and end. Spreading them would drift them off the very lock events they line up with.
    /// </remarks>
    private static bool IsSerialWork(EngineEvent e) =>
        e is not (QueryThreadEvent or MemoryEvent or LockEvent or LockGroup or LockEscalationEvent or TransactionEvent);

}