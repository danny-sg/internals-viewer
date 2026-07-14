using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.UI.App.ViewModels.Query.Events;

internal sealed class PageSpanBuilder
{
    private const long MinFlashDurationUs = 200;

    private readonly Dictionary<PageAddress, PageAddress?> _pageRootCache = new();

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

                            var latchSpan = new PageSpan(latchEvent.PageAddress.Value, latchEvent.TimeUs, endUs, displayColour);

                            pageSpans.Add(latchSpan);
                        }
                    }
                }
                else
                {
                    foreach (var p in group.Pages)
                    {
                        if (p.FileId > 0 && p.FileId <= maxFileId)
                        {
                            pageSpans.Add(new PageSpan(p, readAtUs, queryEndUs, readColour));
                        }
                    }
                }
            }
        }

        return [.. pageSpans.OrderBy(s => s.StartUs)];
    }

    public (Dictionary<PageAddress, List<PageSpan>>, Dictionary<PageAddress, List<PageSpan>>)
        GetIndexPageSpans(List<EngineEvent> engineEvents, EventColourProvider colours, DatabaseSource databaseSource)
    {
        var queryEndUs = ComputeQueryEndUs(engineEvents);

        var allSpans = new Dictionary<PageAddress, List<PageSpan>>();
        var readSpans = new Dictionary<PageAddress, List<PageSpan>>();

        void Add(PageSpan span, bool isRead)
        {
            if (RootPageOf(span.Address, databaseSource) is not { } root)
            {
                return;
            }

            AddSpan(allSpans, root, span);

            if (isRead)
            {
                AddSpan(readSpans, root, span);
            }
        }

        foreach (var e in engineEvents)
        {
            switch (e)
            {
                case LatchEvent { PageAddress: { } pg } latch:
                    var endUs = latch.TimeUs + Math.Max(latch.DurationUs, MinFlashDurationUs);
                    var latchColour = colours.GetLatchMapColour(e.ObjectName) ?? colours.GetColour(e);
                    Add(new PageSpan(pg, latch.TimeUs, endUs, latchColour), isRead: false);
                    break;

                case IoEvent { IsRead: true, PageAddress: { } pg } io:
                    var readColour = colours.GetObjectColour(e.ObjectName) ?? colours.GetColour(e);
                    Add(new PageSpan(pg, io.TimeUs, queryEndUs, readColour), isRead: true);
                    break;

                case ReadEventGroup group:
                    var groupColour = colours.GetObjectColour(e.ObjectName) ?? colours.GetColour(e);
                    var groupReadAtUs = e.TimeUs + e.DurationUs;
                    foreach (var page in group.Pages)
                    {
                        Add(new PageSpan(page, groupReadAtUs, queryEndUs, groupColour), isRead: true);
                    }
                    break;
            }
        }

        return (allSpans, readSpans);

        static void AddSpan(Dictionary<PageAddress, List<PageSpan>> spansByRoot, PageAddress root, PageSpan span)
        {
            if (!spansByRoot.TryGetValue(root, out var list))
            {
                list = [];
                spansByRoot[root] = list;
            }

            list.Add(span);
        }
    }

    private PageAddress? RootPageOf(PageAddress page, DatabaseSource databaseSource)
    {
        if (!_pageRootCache.TryGetValue(page, out var root))
        {
            root = databaseSource.FindPageAllocationUnit(page)?.RootPage;
            _pageRootCache[page] = root;
        }

        return root;
    }

    private static long ComputeQueryEndUs(List<EngineEvent> engineEvents) =>
        engineEvents.DefaultIfEmpty().Max(e => e?.TimeUs + e?.DurationUs) ?? MinFlashDurationUs;
}