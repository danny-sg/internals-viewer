using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.UI.App.Controls.Timeline.Definition;

namespace InternalsViewer.UI.App.Services.Query.Timeline;

internal interface ITimelineBandBuilder
{
    bool Claims(EngineEvent engineEvent);

    TimelineBand? Prepare(IReadOnlyList<EngineEvent> events, TimelineBandVisibility visibility);

    TimelineItem Place(int index, EngineEvent engineEvent, int band, List<TimelineLink> links);
}
