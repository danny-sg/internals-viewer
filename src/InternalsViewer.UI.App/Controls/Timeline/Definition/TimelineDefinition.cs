using System.Collections.Generic;
using InternalsViewer.Query.Events;

namespace InternalsViewer.UI.App.Controls.Timeline.Definition;

public sealed class TimelineDefinition(IReadOnlyList<EngineEvent> events,
                                       IReadOnlyList<TimelineBand> bands,
                                       TimelineItem[] items,
                                       TimelineLink[] links)
{
    public static TimelineDefinition Empty { get; } = new([], [], [], []);

    public IReadOnlyList<EngineEvent> Events { get; } = events;

    public IReadOnlyList<TimelineBand> Bands { get; } = bands;

    public TimelineItem[] Items { get; } = items;

    public TimelineLink[] Links { get; } = links;
}
