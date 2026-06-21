using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Replay.Locks;
using System.Xml.Linq;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Replay.Events;

internal class EventParser
{
    public static void SetRelativeTimestamps(List<EngineEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

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
        {
            return null;
        }

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
        {
            return null;
        }

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
        {
            return null;
        }

        var data = e.Elements("data")
            .ToDictionary(
                d => d.Attribute("name")?.Value ?? string.Empty, object? (d) => d.Element("value")?.Value
            );

        var actions = e.Elements("action")
            .ToDictionary(
                a => a.Attribute("name")?.Value ?? string.Empty, object? (a) => a.Element("value")?.Value
            );

        return new EventResult
        {
            Name = name,
            Timestamp = DateTime.Parse(timestamp),
            Data = data,
            Actions = actions
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
            var n when n.Equals("query_thread_profile") => MapQueryThread(e),
            _ => new EngineEvent
            {
                Name = e.Name,
                Timestamp = e.Timestamp
            }
        };

        engineEvent.SequenceId = e.SequenceId;

        if (e.Actions.TryGetValue("plan_handle", out var planHandle) && planHandle is not null)
        {
            engineEvent.PlanHandle = planHandle.ToString() ?? string.Empty;
        }

        if (database is null)
        {
            return engineEvent;
        }

        if (engineEvent is { ObjectId: 0, PageAddress: not null })
        {
            var allocationUnit = database.FindPageAllocationUnit(engineEvent.PageAddress.Value);

            engineEvent.ObjectId = allocationUnit?.ObjectId ?? 0;
            engineEvent.ObjectName = allocationUnit?.DisplayName
                                     ?? TryGetPageName(engineEvent.PageAddress.Value) ?? string.Empty;

            // A page belongs to exactly one allocation unit, so the index identity here is
            // reliable and lets storage events be matched to a specific seek/scan operator.
            ApplyObjectIdentity(engineEvent, allocationUnit, includeIndex: true);
        }
        else if (engineEvent.ObjectId > 0)
        {
            var allocationUnit = database.AllocationUnits.FirstOrDefault(f => f.ObjectId == engineEvent.ObjectId);

            engineEvent.ObjectName = allocationUnit?.DisplayName ?? $"(Object Id {engineEvent.ObjectId})";

            // Resolved by object id alone (e.g. object-level locks); the specific index is
            // ambiguous, so only the table identity is trusted - match falls back to table level.
            ApplyObjectIdentity(engineEvent, allocationUnit, includeIndex: false);
        }

        return engineEvent;
    }

    private static void ApplyObjectIdentity(EngineEvent engineEvent, AllocationUnit? allocationUnit, bool includeIndex)
    {
        if (allocationUnit is null)
        {
            return;
        }

        engineEvent.SchemaName = allocationUnit.SchemaName;
        engineEvent.TableName = allocationUnit.TableName;

        if (includeIndex)
        {
            engineEvent.IndexName = allocationUnit.IndexName;
        }
    }

    private static string? TryGetPageName(PageAddress pageAddress)
    {
        switch (pageAddress.PageId)
        {
            case 0:
                return "File Header";
            case 9:
                return "Boot page";
            default:
                if (PageHelpers.IsBcm(pageAddress.PageId))
                {
                    return "BCM";
                }

                if (PageHelpers.IsDcm(pageAddress.PageId))
                {
                    return "DCM";
                }

                if (PageHelpers.IsGam(pageAddress.PageId))
                {
                    return "GAM";
                }

                if (PageHelpers.IsSgam(pageAddress.PageId))
                {
                    return "SGAM";
                }

                if (PageHelpers.IsPfs(pageAddress.PageId))
                {
                    return "PFS";
                }

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

    private static EngineEvent MapQueryThread(EventResult e)
    {
        var threadId = (e.GetInt("thread_id") ?? 0);
        var nodeId = (e.GetInt("node_id") ?? 0);

        return new QueryThreadEvent()
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            ThreadId = threadId,
            NodeId = nodeId,
            Duration = e.GetLong("total_time_us") / 1000 ?? 0
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
