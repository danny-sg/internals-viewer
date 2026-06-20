using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace InternalsViewer.Replay.Events;

internal class EventParser
{
    public static void SetRelativeTimestamps(List<EngineEvent> events)
    {
        if (events.Count == 0)
            return;

        var start = events.Min(e => e.Timestamp);

        foreach (var e in events)
        {
            var delta = e.Timestamp - start;

            e.TimeTicks = delta.Ticks;
            e.TimeMs = e.TimeTicks / 1000.0;
        }
    }

    public static EngineEvent? ParseEvent(string xml)
    {
        var element = XElement.Parse(xml);

        var result = ToEventResult(element);

        return result is null ? null : Map(result);
    }

    private static EventResult? ToEventResult(XElement e)
    {
        var name = e.Attribute("name")?.Value;
        var timestampStr = e.Attribute("timestamp")?.Value;

        if (name is null || timestampStr is null)
            return null;

        return new EventResult
        {
            Name = name,
            Timestamp = DateTime.Parse(timestampStr),
            Data = e.Elements("data")
                .ToDictionary(
                    d => d.Attribute("name")?.Value ?? string.Empty,
                    d => (object?)d.Element("value")?.Value
                ),
            Actions = e.Elements("action")
                .ToDictionary(
                    a => a.Attribute("name")?.Value,
                    a => (object?)a.Element("value")?.Value
                )
        };
    }

    private static EngineEvent Map(EventResult e)
    {
        return e.Name switch
        {
            var n when n.Contains("file_") || n.Contains("physical_page")
                => MapIoEvent(e),
            var n when n.Contains("page") => MapPageEvent(e),
            var n when n.Contains("lock_") => MapLock(e),
            var n when n.Contains("wait") => MapWait(e),

            _ => new EngineEvent
            {
                Name = e.Name,
                Timestamp = e.Timestamp
            }
        };
    }

    private static EngineEvent MapWait(EventResult e)
    {
        return new WaitEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            WaitType = e.GetString("wait_type"),
            Duration = e.GetLong("duration") ?? 0
        };
    }

    private static EngineEvent MapLock(EventResult e)
    {
        return new LockEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            Mode = e.GetString("mode"),
            ResourceType = e.GetString("resource_type"),
            FileId = e.GetShort("resource_0") ?? 0,
            PageId = e.GetInt("resource_1") ?? 0
        };
    }

    private static PageEvent MapPageEvent(EventResult e)
    {
        var location = e.GetUlong("page_location") ?? 0;


        var fileId = (short)(location >> 32);

        var rawPageId = (uint)(location & 0xFFFFFFFF);
        var pageId = rawPageId <= int.MaxValue ? (int)rawPageId : 0;

        return new PageEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            FileId = fileId,
            PageId = pageId,
            Type = e.GetString("type")
        };
    }

    private static IoEvent MapIoEvent(EventResult e)
    {
        var offset = e.GetLong("offset") ?? 0;

        int pageId = e.GetInt("page_id") ?? (int)(offset / 8192);
        
        return new IoEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            FileId = e.GetShort("file_id") ?? 0,
            PageId = pageId,
            IsRead = e.Name?.Contains("read") ?? false
        };
    }
}
