using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Controls.Timeline.Definition;

namespace InternalsViewer.UI.App.Services.Query.Timeline;

/// <summary>
/// Builds lanes from events
/// </summary>
/// <remarks>
/// Lane structure is:
///
///     Band → Sub Band → Lane → Track
/// </remarks>
internal sealed class EventBandBuilder<TEvent>(TimelineBand band,
                                               Func<IReadOnlyList<EngineEvent>, TimelineBandVisibility, bool> isShown,
                                               bool drawsMarkers = true,
                                               bool linksToOperator = false) : ITimelineBandBuilder
    where TEvent : EngineEvent
{
    public bool Claims(EngineEvent engineEvent) => engineEvent is TEvent;

    public TimelineBand Prepare(IReadOnlyList<EngineEvent> events) => band;

    public bool IsShown(IReadOnlyList<EngineEvent> events, TimelineBandVisibility visibility) => isShown(events, visibility);

    public TimelineItem Place(int index, EngineEvent engineEvent, int bandIndex)
    {
        if (!drawsMarkers)
        {
            return TimelineItem.Undrawn(bandIndex);
        }

        if (engineEvent.Category is { } category)
        {
            return new TimelineItem(bandIndex,
                                    (int)category,
                                    EventCategoryClassifier.CategoryCount,
                                    1,
                                    TimelineFill.Translucent,
                                    TimelineTickAnchor.Start,
                                    TimelineColourSource.Fixed,
                                    TimelineColours.TintByCategory(band.Colour, (int)category),
                                    0f);
        }

        return new TimelineItem(bandIndex,
                                0,
                                1,
                                0,
                                TimelineFill.Translucent,
                                TimelineTickAnchor.Start,
                                TimelineColourSource.Provider,
                                band.Colour,
                                0f);
    }

    public void AddLinks(int index, EngineEvent engineEvent, List<TimelineLink> links)
    {
        if (linksToOperator && engineEvent.PlanNodeIdentifier is { } node)
        {
            links.Add(new TimelineLink(index, -1, node, TimelineLinkStyle.Bar, 1));
        }
    }
}
