using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Transactions;

namespace InternalsViewer.UI.App.ViewModels.Query.Events;

internal static class PageSpanBuilder
{
    private const long MinFlashDurationUs = 200;

    public static List<PageSpan> GetEventsPageSpans(List<EngineEvent> engineEvents,
                                                    EventColourProvider colours,
                                                    long? startOffset,
                                                    long? endOffset,
                                                    DatabaseSource databaseSource)
    {
        var maxFileId = databaseSource.Files.Max(d => d.FileId);

        var queryEndUs = ComputeQueryEndUs(engineEvents);

        var pageSpans = new List<PageSpan>();

        foreach (var e in engineEvents)
        {
            if (startOffset != null && endOffset != null && (e.TimeUs < startOffset || e.TimeUs > endOffset))
            {
                continue;
            }

            if (e is ReadEventGroup group)
            {
                AddReadEventGroupSpans(group, e, colours, queryEndUs, maxFileId, pageSpans.Add);
            }

            if (e is TransactionLogEvent logEvent)
            {
                AddLogEventSpan(logEvent, queryEndUs, pageSpans.Add);
            }
        }

        return [.. pageSpans.OrderBy(s => s.StartUs)];
    }

    private static void AddLogEventSpan(TransactionLogEvent logEvent, long queryEndUs, Action<PageSpan> add)
    {
        if (logEvent.PageAddress is not null)
        {
            add(new PageSpan(logEvent.PageAddress.Value, logEvent.TimeUs, queryEndUs, ColourConstants.LogColour));
        }
    }

    private static void AddReadEventGroupSpans(ReadEventGroup group,
                                               EngineEvent e,
                                               EventColourProvider colours,
                                               long queryEndUs,
                                               int maxFileId,
                                               Action<PageSpan> add)
    {
        var readColour = colours.GetObjectColour(e.ObjectName) ?? colours.GetColour(e);

        var readAtUs = e.TimeUs + e.DurationUs;

        if (group.ReadType == ReadType.Cached)
        {
            foreach (var readEvent in group.Events)
            {
                if (readEvent is LatchEvent { PageAddress.FileId: > 0 } latchEvent)
                {
                    var displayColour = colours.GetLatchMapColour(e.ObjectName)
                                        ?? colours.GetObjectColour(e.ObjectName)
                                        ?? colours.GetColour(e);

                    var endUs = latchEvent.TimeUs + Math.Max(latchEvent.DurationUs, MinFlashDurationUs);

                    add(new PageSpan(latchEvent.PageAddress.Value, latchEvent.TimeUs, endUs, displayColour));
                }
            }
        }
        else
        {
            foreach (var p in group.Pages)
            {
                if (p.FileId > 0 && p.FileId <= maxFileId)
                {
                    add(new PageSpan(p, readAtUs, queryEndUs, readColour));
                }
            }
        }
    }

    private static long ComputeQueryEndUs(List<EngineEvent> engineEvents) =>
        engineEvents.DefaultIfEmpty().Max(e => e?.TimeUs + e?.DurationUs) ?? MinFlashDurationUs;
}