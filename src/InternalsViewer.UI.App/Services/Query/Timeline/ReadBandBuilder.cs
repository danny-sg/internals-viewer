using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using SkiaSharp;

namespace InternalsViewer.UI.App.Services.Query.Timeline;

internal sealed class ReadBandBuilder : ITimelineBandBuilder
{
    private static readonly TimelineBand Band = new(typeof(ReadEventGroup),
                                                    "Read",
                                                    ColourConstants.IoColour.ToSkColor().WithAlpha(255),
                                                    0.5f)
    {
        SubBandLabels = new TimelineSubBandLabels("Buffer", "Read", "Disk"),
    };

    private static readonly SKColor AllocationPageColour = ColourConstants.AllocationPageColour.ToSkColor();

    private Dictionary<ObjectPoolEvent, int> PoolIndexes { get; } = new(ReferenceEqualityComparer.Instance);

    public bool Claims(EngineEvent engineEvent) => engineEvent is ReadEventGroup or IoEvent;

    public TimelineBand? Prepare(IReadOnlyList<EngineEvent> events, TimelineBandVisibility visibility)
    {
        PoolIndexes.Clear();

        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is ObjectPoolEvent pool)
            {
                PoolIndexes[pool] = i;
            }
        }

        return Band;
    }

    public TimelineItem Place(int index, EngineEvent engineEvent, int band, List<TimelineLink> links)
    {
        AddLink(index, engineEvent, links);

        var isCached = engineEvent is ReadEventGroup { ReadType: ReadType.Cached };

        var isAllocationPage = engineEvent is ReadEventGroup { IsAllocationPage: true };

        return new TimelineItem(band,
                                isCached ? 0 : 1,
                                2,
                                1,
                                TimelineFill.Translucent,
                                engineEvent is ReadEventGroup ? TimelineTickAnchor.End : TimelineTickAnchor.Start,
                                isAllocationPage ? TimelineColourSource.Fixed : TimelineColourSource.Provider,
                                isAllocationPage ? AllocationPageColour : Band.Colour,
                                0f);
    }

    private void AddLink(int index, EngineEvent engineEvent, List<TimelineLink> links)
    {
        if (engineEvent is not ReadEventGroup { IsAllocationPage: false } read)
        {
            return;
        }

        var pool = read.PoolLookup is { } lookup && PoolIndexes.TryGetValue(lookup, out var poolIndex) ? poolIndex : -1;

        if (pool < 0 && read.PlanNodeIdentifier is null)
        {
            return;
        }

        links.Add(new TimelineLink(index, pool, read.PlanNodeIdentifier, TimelineLinkStyle.CallReturn, Math.Max(1, read.PageCount)));
    }
}
