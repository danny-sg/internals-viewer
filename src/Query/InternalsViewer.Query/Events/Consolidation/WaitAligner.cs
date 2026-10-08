using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Waits;

namespace InternalsViewer.Query.Events.Consolidation;

/// <summary>
/// Aligns page IO waits onto the latch suspend that measures them
/// </summary>
/// <remarks>
/// A PAGEIOLATCH wait and the BUF latch suspend it sits inside are one pause seen twice: the suspend is the latch giving up
/// the CPU while the read runs, the wait is what the scheduler records for the same pause. They are linked by address —
/// wait_resource on a page IO wait is the address of the BUF latch being suspended on.
///
/// The suspend is the only microsecond-accurate side of the pair. wait_info reports its duration in milliseconds, so every
/// sub-millisecond read truncates to a zero-duration wait, and the begin/end timestamps are no help either — both halves of
/// a short wait land in the same millisecond bucket. So the wait takes the suspend's window and its page rather than
/// anything measured on the wait itself.
///
/// Must run after <see cref="IntervalCollapser"/>, which is what gives the suspend its folded duration, and before
/// <see cref="ReaderGrouper"/>, which matches waits to reads on those timings.
/// </remarks>
public static class WaitAligner
{
    public static void Align(IReadOnlyList<EngineEvent> events)
    {
        var suspends = new Dictionary<ulong, List<LatchEvent>>();

        foreach (var latch in events.OfType<LatchEvent>())
        {
            if (latch is { Name: "latch_suspend_begin", LatchAddress: { } address })
            {
                if (!suspends.TryGetValue(address, out var list))
                {
                    list = [];

                    suspends[address] = list;
                }

                list.Add(latch);
            }
        }

        if (suspends.Count == 0)
        {
            return;
        }

        foreach (var wait in events.OfType<WaitEvent>())
        {
            if (!wait.WaitType.IsPageIoLatchWait()
                || wait.WaitResource is not { } resource
                || !suspends.TryGetValue(resource, out var candidates))
            {
                continue;
            }

            // A buffer is latched many times over a query, so the address alone identifies the frame rather than the pause. Nearest in
            // time picks the suspend this wait belongs to.
            var suspend = candidates.MinBy(s => Math.Abs(s.TimeUs - wait.TimeUs));

            if (suspend is null)
            {
                continue;
            }

            wait.TimeUs = suspend.TimeUs;

            wait.DurationUs = suspend.DurationUs;

            wait.PageAddress = suspend.PageAddress;
        }
    }
}
