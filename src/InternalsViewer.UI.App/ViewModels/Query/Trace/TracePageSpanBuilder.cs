using System;
using System.Collections.Generic;
using System.Drawing;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.UI.App.Models.Index;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

internal static class TracePageSpanBuilder
{
    private const int StepBits = 32;

    public static long GetPosition(TracePosition position) => (long)(position.Sequence << StepBits | position.Steps);

    public static (List<PageSpan> Spans, List<PageVisit> Visits) Build(TracePageAccessList accesses,
                                                                      short databaseId,
                                                                      Func<PageAddress, Color> getColour)
    {
        var spans = new List<PageSpan>();

        var visits = new List<PageVisit>();

        var colours = new Dictionary<PageAddress, Color>();

        for (var index = 0; index < accesses.Count; index++)
        {
            var access = accesses[index];

            if (access.DatabaseId != databaseId)
            {
                continue;
            }

            var position = GetPosition(access.Position);

            visits.Add(new PageVisit(position, access.Page, access.Offset));

            if (access.Offset is not null)
            {
                continue;
            }

            if (!colours.TryGetValue(access.Page, out var colour))
            {
                colour = getColour(access.Page);

                colours[access.Page] = colour;
            }

            spans.Add(new PageSpan(access.Page, position, long.MaxValue, colour));
        }

        return (spans, visits);
    }
}
