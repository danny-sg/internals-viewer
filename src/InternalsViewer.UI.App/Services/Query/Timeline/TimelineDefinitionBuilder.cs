using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.Query.Events.Waits;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using SkiaSharp;
using InternalsViewer.UI.App.Controls.Timeline.Definition;

namespace InternalsViewer.UI.App.Services.Query.Timeline;

internal sealed class TimelineDefinitionBuilder(IReadOnlyList<ITimelineBandBuilder> builders)
{
    public static TimelineDefinitionBuilder CreateDefault() => new(
    [
        new EventBandBuilder<TransactionLogEvent>(
            new TimelineBand(typeof(TransactionLogEvent), "Log", ColourConstants.LogColour.ToSkColor().WithAlpha(255), 0.5f),
            (events, _) => events.Any(e => e is TransactionLogEvent),
            linksToOperator: true),
        new EventBandBuilder<ExecutionOperatorEvent>(
            new TimelineBand(typeof(ExecutionOperatorEvent), "Plan", SKColors.LimeGreen, 3f),
            (_, _) => true,
            drawsMarkers: false),
        new ColumnstoreBandBuilder(),
        new ReadBandBuilder(),
        new EventBandBuilder<LockEvent>(
            new TimelineBand(typeof(LockEvent), "Lock", ColourConstants.LockColour.ToSkColor().WithAlpha(255), 0.5f),
            (events, visibility) => visibility.ShowLocks && events.Any(e => e is LockEvent or LockGroup),
            drawsMarkers: false),
        new EventBandBuilder<LatchEvent>(
            new TimelineBand(typeof(LatchEvent), "Latch", ColourConstants.LatchColour.ToSkColor().WithAlpha(255), 0.167f),
            (events, visibility) => visibility.ShowLatches && events.Any(e => e is LatchEvent)),
        new EventBandBuilder<WaitEvent>(
            new TimelineBand(typeof(WaitEvent), "Wait", ColourConstants.WaitColour.ToSkColor().WithAlpha(255), 0.5f),
            (events, visibility) => visibility.ShowWaits && events.Any(e => e is WaitEvent)),
    ]);

    public TimelineDefinition Build(IReadOnlyList<EngineEvent> source,
                                    TimelineBandVisibility visibility,
                                    Func<EngineEvent, bool>? isVisible = null)
    {
        IReadOnlyList<EngineEvent> events = [.. ExpandGroupedEvents(source, isVisible).OrderBy(e => e.SequenceId)];

        var bands = new List<TimelineBand>(builders.Count);

        var bandOfBuilder = new int[builders.Count];

        for (var b = 0; b < builders.Count; b++)
        {
            var band = builders[b].Prepare(events, visibility);

            bandOfBuilder[b] = band is null ? -1 : bands.Count;

            if (band is not null)
            {
                bands.Add(band);
            }
        }

        var items = new TimelineItem[events.Count];

        var links = new List<TimelineLink>();

        Array.Fill(items, TimelineItem.Unplaced);

        for (var i = 0; i < events.Count; i++)
        {
            var engineEvent = events[i];

            for (var b = 0; b < builders.Count; b++)
            {
                if (!builders[b].Claims(engineEvent))
                {
                    continue;
                }

                if (bandOfBuilder[b] >= 0)
                {
                    items[i] = builders[b].Place(i, engineEvent, bandOfBuilder[b], links);
                }

                break;
            }
        }

        return new TimelineDefinition(events, bands, items, [.. links]);
    }

    private static IEnumerable<EngineEvent> ExpandGroupedEvents(IReadOnlyList<EngineEvent> events, Func<EngineEvent, bool>? isVisible)
    {
        foreach (var engineEvent in events)
        {
            if (isVisible is not null && !isVisible(engineEvent))
            {
                continue;
            }

            yield return engineEvent;

            if (engineEvent is not ReadEventGroup readGroup)
            {
                continue;
            }

            foreach (var member in readGroup.Events)
            {
                if (member is not FileEvent)
                {
                    yield return member;
                }
            }
        }
    }
}
