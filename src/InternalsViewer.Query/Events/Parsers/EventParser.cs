using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Waits;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.TransactionLog;

namespace InternalsViewer.Query.Events.Parsers;

public sealed class EventParser
{
    public EngineEvent? ToEngineEvent(EventResult e,
                                      DatabaseSource? database,
                                      PlanHandleRegistry planHandles,
                                      CallStackTree callStack)
    {
        var engineEvent = e.Name switch
        {
            var n when n.Contains("file_")
                => MapFileEvent(e),
            var n when n.Contains("physical_page")
                => MapIoEvent(e),
            var n when n.Contains("lock_")
                => LockEventParser.MapLock(e),
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

        var taskAddress = e.GetUlongAction("task_address");

        var workerAddress = e.GetUlongAction("worker_address");

        var sequenceId = e.GetInt("event_sequence");

        engineEvent.WorkerAddress = workerAddress;
        engineEvent.TaskAddress = taskAddress;
        engineEvent.SequenceId = (sequenceId * 10) ?? e.SequenceId;

        if (e.Actions.TryGetValue("plan_handle", out var planHandle) && planHandle.Length > 0)
        {
            engineEvent.PlanHandleId = planHandles.GetOrAdd(e.Buffer.AsSpan(planHandle.Offset, planHandle.Length));
        }

        if (database is null)
        {
            return engineEvent;
        }

        // Object identity is a reference to the shared allocation unit; names are read from it on demand (page-only,
        // object-id-only and special-page fallbacks live on the event subtypes).
        if (engineEvent is LockEvent lockEvent)
        {
            // A lock resolves to its object's allocation unit: an object lock carries object_id directly, while a
            // finer-grained lock (page/rid/key) carries only the HoBT (partition), so those resolve via the partition
            // id. Either way the resolved AllocationUnit.ObjectId is the consistent object an escalation groups on.
            engineEvent.AllocationUnit = lockEvent.Resource.ObjectId > 0
                ? database.FindObjectIdAllocationUnit(lockEvent.Resource.ObjectId)
                : lockEvent.Resource.HobtId is { } hobtId and > 0
                    ? database.FindHobtIdAllocationUnit(hobtId)
                    : null;
        }
        else if (engineEvent is PageEngineEvent { ObjectId: 0, PageAddress: { } pageAddress } pageEvent)
        {
            var allocationUnit = database.FindPageAllocationUnit(pageAddress);

            engineEvent.AllocationUnit = allocationUnit;

            if (pageEvent is IoEvent ioEvent && allocationUnit?.RootPage == pageAddress)
            {
                ioEvent.IsRoot = true;
            }
        }
        else if (engineEvent.ObjectId > 0)
        {
            engineEvent.AllocationUnit = database.FindObjectIdAllocationUnit(engineEvent.ObjectId);
        }
        else if (engineEvent is TransactionLogEvent { AllocationUnitId: > 0 } logEvent)
        {
            engineEvent.AllocationUnit = database.AllocationUnits.TryGetValue(logEvent.AllocationUnitId, out var value)
                ? value
                : AllocationUnit.Unknown;
        }

        engineEvent.Category = EventCategoryClassifier.GetCategory(engineEvent);

        if (e.Actions.ContainsKey("callstack"))
        {
            var frames = XmlCallStackParser.ParseCallstack(e.GetStringAction("callstack"));

            if (frames.Count > 0)
            {
                engineEvent.CallStack = callStack.Add(frames, engineEvent);
            }
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

        if (EventFilter.CanIgnore(waitType.ToString()))
        {
            return null;
        }

        var isEnd = e.GetString("opcode") == "End";

        var waitResource = e.GetUlong("wait_resource");

        var duration = e.GetLong("duration") ?? 0;

        var waitEvent = new WaitEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            WaitType = waitType,
            IsEnd = isEnd,
            WaitResource = waitResource,
            DurationUs = duration,
        };

        return waitEvent;
    }

    private EngineEvent? MapLatch(EventResult e)
    {
        var address = e.GetUlong("address");

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
            LatchAddress = address,
            DurationUs = e.GetLong("duration") ?? 0,
            PageAddress = new PageAddress(fileId, pageId)
        };

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

    private static IoEvent MapIoEvent(EventResult e)
    {
        var fileId = e.GetShort("file_id") ?? 0;
        var pageId = e.GetInt("page_id") ?? 0;

        return new IoEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            PageAddress = new PageAddress(fileId, pageId),
            IsRead = e.Name?.Contains("read") ?? false
        };
    }

    private static FileEvent MapFileEvent(EventResult e)
    {
        var offset = e.GetLong("offset") ?? 0;
        var size = e.GetLong("size") ?? 0;

        var fileId = e.GetShort("file_id") ?? 0;

        var pageId = e.GetInt("page_id") ?? (int)(offset / 8192);

        var mode = (ReadMode)(e.GetByte("mode") ?? 0);

        return new FileEvent
        {
            Name = e.Name,
            Size = size,
            Offset = offset,
            Mode = mode,
            FileId = fileId,
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
}