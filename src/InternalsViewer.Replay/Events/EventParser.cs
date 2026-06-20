using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Replay.Locks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace InternalsViewer.Replay.Events;

internal class EventParser
{
    public static void SetRelativeTimestamps(List<EngineEvent> events)
    {
        if (events.Count == 0)
            return;

        var ordered = events.OrderBy(e => e.SequenceId).ToList();

        var start = ordered[0].Timestamp;

        foreach (var e in ordered)
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

        if (result == null)
            return null;

        if (result.Actions.TryGetValue("sql_text", out var value) && $"{value}".StartsWith("ALTER EVENT SESSION"))
        {
            return null;
        }

        return ToEngineEvent(result, null);
    }

    public static EngineEvent? ParseEvent(string xml, DatabaseSource database)
    {
        var element = XElement.Parse(xml);

        var result = ToEventResult(element);

        if (result == null)
            return null;

        if (result.Actions.TryGetValue("sql_text", out var value) && $"{value}".StartsWith("ALTER EVENT SESSION"))
        {
            return null;
        }

        var engineEvents = ToEngineEvent(result, database);

        return engineEvents;
    }

    private static EventResult? ToEventResult(XElement e)
    {
        var name = e.Attribute("name")?.Value;
        var timestamp = e.Attribute("timestamp")?.Value;

        if (name is null || timestamp is null)
            return null;

        return new EventResult
        {
            Name = name,
            Timestamp = DateTime.Parse(timestamp),
            Data = e.Elements("data")
                .ToDictionary(
                    d => d.Attribute("name")?.Value ?? string.Empty,
                    d => (object?)d.Element("value")?.Value
                ),
            Actions = e.Elements("action")
                .ToDictionary(
                    a => a.Attribute("name")?.Value ?? string.Empty,
                    a => (object?)a.Element("value")?.Value
                )
        };
    }

    private static EngineEvent ToEngineEvent(EventResult e, DatabaseSource? database)
    {
        var engineEvent = e.Name switch
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

        engineEvent.SequenceId = e.SequenceId;

        if (database is null)
        {
            return engineEvent;
        }

        if (engineEvent is { ObjectId: 0, PageAddress: not null })
        {
            var allocationUnit = database.FindPageAllocationUnit(engineEvent.PageAddress.Value);

            engineEvent.ObjectId = allocationUnit?.ObjectId ?? 0;
            engineEvent.ObjectName = allocationUnit?.DisplayName ?? TryGetPageName(engineEvent.PageAddress.Value) ?? string.Empty;
        }
        else if (engineEvent.ObjectId > 0)
        {
            var allocationUnit = database.AllocationUnits.FirstOrDefault(f => f.ObjectId == engineEvent.ObjectId);

            engineEvent.ObjectName = allocationUnit?.DisplayName ?? $"(Object Id {engineEvent.ObjectId})";
        }

        return engineEvent;
    }

    private static string? TryGetPageName(PageAddress pageAddress)
    {
        switch (pageAddress.PageId)
        {
            case 0:
                return "File Header";
            
            case 1:
                return  "PFS";
            case 2:
              return "GAM";

            case 3:
                return "SGAM";

            case 4:
                return "DCM";

            case 5:
                return "BCM";

            case 6:
                return "Differential Change Map";

            case 7:
                return  "Bulk Change Map";
            case 9:
                return "Boot page";

            default:
                return null;
        }
    }

    private static EngineEvent MapWait(EventResult e)
    {
        var waitType = (WaitType)(e.GetInt("wait_type") ?? 0);

        return new WaitEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            WaitType = waitType,
            Duration = e.GetLong("duration") ?? 0
        };
    }

    private static EngineEvent MapLock(EventResult e)
    {
        var lockMode = (LockMode)(e.GetInt("mode") ?? 0);
        var resourceType = (LockResourceType)(e.GetInt("resource_type") ?? 0);

        var resource0 = e.GetUlong("resource_0") ?? 0;
        var resource1 = e.GetUlong("resource_1") ?? 0;
        var resource2 = e.GetUlong("resource_2") ?? 0;

        var lockEvent = new LockEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            LockMode = lockMode,
            ResourceType = (LockResourceType)(e.GetInt("resource_type") ?? 0),
            ObjectId = e.GetInt("object_id") ?? 0
        };

        return resourceType switch
        {
            LockResourceType.Page =>
                lockEvent with
                {
                    PageAddress = new PageAddress((short)resource0, (int)resource1)
                },
            LockResourceType.Rid =>
                lockEvent with
                {
                    RowIdentifier = new RowIdentifier((short)resource0, (int)resource1, (ushort)resource2),
                },
            LockResourceType.Key =>
                lockEvent with
                {
                    KeyHash = $"({resource0:x})"
                },

            _ => lockEvent
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
            PageAddress = new PageAddress(fileId, pageId),
            Type = e.GetString("type")
        };
    }

    private static IoEvent MapIoEvent(EventResult e)
    {
        var offset = e.GetLong("offset") ?? 0;

        var fileId = e.GetShort("file_id") ?? 0;
        int pageId = e.GetInt("page_id") ?? (int)(offset / 8192);

        return new IoEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            PageAddress = new PageAddress(fileId, pageId),
            IsRead = e.Name?.Contains("read") ?? false
        };
    }
}
