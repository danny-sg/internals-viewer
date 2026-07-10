using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Locks;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.TransactionLog;

namespace InternalsViewer.Query.Events.Parsers;

internal record WaitKey(ulong TaskAddress, WaitType WaitType);

public sealed class EventParser
{
    private readonly Dictionary<ulong, LatchEvent> _latches = new();

    private readonly Dictionary<WaitKey, WaitEvent> _waits = new();

    public EngineEvent? ToEngineEvent(EventResult e, DatabaseSource? database, PlanHandleRegistry planHandles)
    {
        var engineEvent = e.Name switch
        {
            var n when n.Contains("file_") || n.Contains("physical_page")
                => MapIoEvent(e),
            var n when n.Contains("page")
                => MapPageEvent(e),
            var n when n.Contains("lock_")
                => MapLock(e),
            var n when n.Contains("wait")
                => MapWait(e),
            var n when n.Contains("latch")
                => MapLatch(e),
            "query_thread_profile"
                => MapQueryThread(e),
            "query_memory_grant_usage"
                => MapMemory(e),
            "hash_spill_details"
                => MapMemory(e),
            "memory_grant_updated_by_feedback"
                => MapMemory(e),
            "sort_warning"
                => MapMemory(e),
            "transaction_log"
                => MapTransactionLogEvent(e),
            "sql_batch_starting"
                => MapBatchStart(e),
            _ => new EngineEvent
            {
                Name = e.Name,
                Timestamp = e.Timestamp
            }
        };

        if (engineEvent is null)
        {
            return engineEvent;
        }

        var sequenceId = e.GetInt("event_sequence");

        engineEvent.SequenceId = (sequenceId * 10) ?? e.SequenceId;

        if (e.Actions.TryGetValue("plan_handle", out var planHandle) && planHandle.Length > 0)
        {
            engineEvent.PlanHandleId = planHandles.GetOrAdd(e.Buffer.AsSpan(planHandle.Offset, planHandle.Length));
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

            if (engineEvent is IoEvent ioEvent && allocationUnit?.RootPage == engineEvent.PageAddress)
            {
                ioEvent.IsRoot = true;
            }

            ApplyObjectIdentity(engineEvent, allocationUnit, includeIndex: true);
        }
        else if (engineEvent.ObjectId > 0)
        {
            var allocationUnit = database.AllocationUnits
                .Values
                .FirstOrDefault(f => f.ObjectId == engineEvent.ObjectId);

            engineEvent.ObjectName = allocationUnit?.DisplayName ?? $"(Object Id {engineEvent.ObjectId})";

            if (engineEvent is IoEvent ioEvent && allocationUnit?.RootPage == engineEvent.PageAddress)
            {
                ioEvent.IsRoot = true;
            }

            ApplyObjectIdentity(engineEvent, allocationUnit, includeIndex: false);
        }
        else if (engineEvent is TransactionLogEvent { AllocationUnitId: > 0 } logEvent)
        {
            var allocationUnit = database.AllocationUnits.TryGetValue(logEvent.AllocationUnitId, out var value)
                ? value
                : AllocationUnit.Unknown;

            engineEvent.ObjectId = allocationUnit?.ObjectId ?? 0;
            engineEvent.ObjectName = allocationUnit?.DisplayName ?? string.Empty;

            ApplyObjectIdentity(engineEvent, allocationUnit, includeIndex: true);
        }

        engineEvent.Category = EventCategoryClassifier.GetCategory(engineEvent);

        if (e.Actions.ContainsKey("callstack"))
        {
            engineEvent.Callstack = ParseCallstack(e.GetStringAction("callstack"));
        }

        return engineEvent;
    }

    private static BatchStartEvent MapBatchStart(EventResult e)
    {
        return new BatchStartEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            SqlText = e.GetString("batch_text")
        };
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

    private static EngineEvent MapMemory(EventResult e)
    {
        switch (e.Name)
        {
            case "memory_grant_updated_by_feedback":
                return new MemoryEvent
                {
                    Name = e.Name,
                    Timestamp = e.Timestamp,
                    DatabaseId = e.GetDatabaseId(),
                    AdditionalMemoryBeforeKb = e.GetLong("ideal_additional_memory_before_kb") ?? 0,
                    AdditionalMemoryAfterKb = e.GetLong("ideal_additional_memory_after_kb") ?? 0,
                    DurationUs = e.GetLong("duration") ?? 0
                };
            default:
                return new MemoryEvent
                {
                    Name = e.Name,
                    Timestamp = e.Timestamp,
                    DatabaseId = e.GetDatabaseId(),
                    UsedMemoryKb = e.GetLong("used_memory_kb") ?? 0,
                    GrantedMemoryKb = e.GetLong("granted_memory_kb") ?? 0,
                    DurationUs = e.GetLong("duration") ?? 0
                };
        }
    }

    private WaitEvent? MapWait(EventResult e)
    {
        var waitType = (WaitType)(e.GetInt("wait_type") ?? 0);
        var taskAddress = e.GetUlongAction("task_address");

        WaitKey? key = null;

        if (taskAddress.HasValue)
        {
            key = new WaitKey(taskAddress.Value, waitType);
        }

        if (key != null && e.Name == "wait_completed")
        {
            if (_waits.Remove(key, out var completed))
            {
                completed.DurationUs = e.GetLong("duration") ?? completed.DurationUs;
            }

            return null;
        }

        var waitEvent = new WaitEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            WaitType = waitType,
            DurationUs = e.GetLong("duration") ?? 0
        };

        // Link to an existing latch via wait_resource
        if ((waitType.IsPageLatchWait() || waitType.IsPageIoLatchWait())
            && _latches.TryGetValue(e.GetUlong("wait_resource") ?? 0, out var latch))
        {
            waitEvent.LatchEvent = latch;
        }

        if (key != null)
        {
            _waits[key] = waitEvent;
        }

        return waitEvent;
    }

    private EngineEvent? MapLatch(EventResult e)
    {
        var address = e.GetUlong("address");

        if (e.Name is "latch_suspend_begin" or "latch_suspend_end" && address is not null)
        {
            if (_latches.TryGetValue(address.Value, out var latch))
            {
                latch.IsSuspended = e.Name == "latch_suspend_begin";

                var duration = e.GetLong("duration");

                if (!latch.IsSuspended && duration is not null)
                {
                    latch.DurationUs = duration.Value;
                    latch.TimeUs -= duration.Value;
                }
            }

            return null;
        }

        var latchMode = (LatchMode)(e.GetInt("mode") ?? 0);

        var fileId = e.GetShort("file_id") ?? 0;

        var pageId = e.GetInt("page_id") ?? 0;

        var latchClass = (LatchClass)(e.GetInt("class") ?? 0);

        var latchEvent = new LatchEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            LatchMode = latchMode,
            LatchClass = latchClass,
            DurationUs = e.GetLong("duration") ?? 0,
            PageAddress = new PageAddress(fileId, pageId)
        };

        if (address is not null)
        {
            _latches[address.Value] = latchEvent;
        }

        return latchEvent;
    }

    private static EngineEvent MapQueryThread(EventResult e)
    {
        var threadId = (e.GetInt("thread_id") ?? 0);
        var nodeId = (e.GetInt("node_id") ?? 0);

        return new QueryThreadEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            ThreadId = threadId,
            NodeId = nodeId,
            DurationUs = e.GetLong("total_time_us") ?? 0
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

        var pageId = e.GetInt("page_id") ?? (int)(offset / 8192);

        return new IoEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            PageAddress = new PageAddress(fileId, pageId),
            IsRead = e.Name?.Contains("read") ?? false
        };
    }

    private static TransactionLogEvent MapTransactionLogEvent(EventResult e)
    {
        return new TransactionLogEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            Operation = (LogOperation)(e.GetInt("operation") ?? 0),
            Context = (LogContext)(e.GetInt("context") ?? 0),
            AllocationUnitId = e.GetLong("alloc_unit_id") ?? 0,
            TransactionId = e.GetInt("transaction_id")
        };
    }

    private static List<CallstackFrame> ParseCallstack(string decoded)
    {
        var frames = new List<CallstackFrame>();
        var xml = decoded.AsSpan();
        var i = 0;

        while (i < xml.Length)
        {
            var offset = xml[i..].IndexOf("<frame".AsSpan(), StringComparison.Ordinal);

            if (offset < 0)
            {
                break;
            }

            i += offset;

            var tagEnd = XmlEventTagParser.FindTagEnd(xml, i);

            if (tagEnd < 0)
            {
                break;
            }

            var tag = xml[i..(tagEnd + 1)];
            var module = XmlEventAttributeParser.GetAttribute(tag, "module");
            var pdb = XmlEventAttributeParser.GetAttribute(tag, "pdb");
            var guid = XmlEventAttributeParser.GetAttribute(tag, "guid");
            var ageSpan = XmlEventAttributeParser.GetAttribute(tag, "age");
            var rvaSpan = XmlEventAttributeParser.GetAttribute(tag, "rva");

            if (!module.IsEmpty)
            {
                int.TryParse(ageSpan, out var age);

                var rvaValue = !rvaSpan.IsEmpty && rvaSpan.Length > 2 && rvaSpan[1] is 'x' or 'X'
                    ? uint.TryParse(rvaSpan[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex) ? hex : 0U
                    : uint.TryParse(rvaSpan, out var dec) ? dec : 0U;

                frames.Add(new CallstackFrame
                {
                    Module = module.ToString(),
                    Pdb = pdb.ToString(),
                    Guid = guid.ToString(),
                    Age = age,
                    Rva = rvaValue
                });
            }

            i = tagEnd + 1;
        }

        return frames;
    }
}