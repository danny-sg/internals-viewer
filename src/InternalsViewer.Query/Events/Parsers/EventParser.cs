using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Batches;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Memory;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.Query.Events.Waits;
using InternalsViewer.Query.Interfaces.Events;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.Query.TransactionLog;

namespace InternalsViewer.Query.Events.Parsers;

public sealed class EventParser
{
    // Call stack module, pdb and guid values come from a handful of distinct strings shared across every event, so
    // they're interned once here rather than reallocated per frame (see StringInternPool).
    private readonly StringInternPool _frameStrings = new();

    public EngineEvent? ToEngineEvent(EventResult e,
                                      DatabaseSource? database,
                                      PlanHandleRegistry planHandles,
                                      CallStackTree callStack)
    {
        var engineEvent = e.Name switch
        {
            var n when n.Contains("file_")
                => FileEventParser.Map(database!, e),
            var n when n.Contains("physical_page")
                => IoEventParser.Map(database!, e),
            "lock_escalation"
                => LockEventParser.MapEscalation(database!, e),
            var n when n.Contains("lock_")
                => LockEventParser.Map(database!, e),
            var n when n.Contains("wait")
                => WaitEventParser.Map(database!, e),
            var n when n.Contains("latch")
                => LatchEventParser.Map(database!, e),
            "query_thread_profile" 
                or "query_memory_grant_usage" 
                or "hash_spill_details" 
                or "memory_grant_updated_by_feedback" 
                or "sort_warning"
                => QueryThreadParser.Map(database!, e),
            "transaction_log"
                => TransactionEventParser.Map(database!, e),
            "sql_transaction"
                => TransactionEventParser.Map(database!, e),
            "sql_batch_starting"
                => BatchStartEventParser.Map(database!, e),
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
            
            // No-op - this will be refactored as the LockEventParser manages this
       
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

        if (e.TryGetActionSpan("callstack", out var callstack))
        {
            var frames = XmlCallStackParser.ParseCallstack(callstack, _frameStrings);

            if (frames.Count > 0)
            {
                engineEvent.CallStack = callStack.Add(frames, engineEvent);
            }
        }

        return engineEvent;
    }

    private static TransactionEvent MapTransaction(EventResult e)
    {
        return new TransactionEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            TransactionId = e.GetLong("transaction_id") ?? 0,
            State = (TransactionState)(e.GetInt("transaction_state") ?? 0),
        };
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

        if (WaitEventFilter.CanIgnore(waitType.ToString()))
        {
            return null;
        }

        var isEnd = e.GetString("opcode") == "End";

        var waitResource = e.GetUlong("wait_resource");

        var duration = e.GetLong("duration") ?? 0;

        var waitEvent = new WaitEvent
        {
            Name = "Wait",
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            WaitType = waitType,
            IsEnd = isEnd,
            WaitResource = waitResource,
            DurationUs = duration,
        };

        return waitEvent;
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

internal class BatchStartEventParser: IEventParser<BatchStartEvent> 
{
    public static BatchStartEvent Map(DatabaseSource databaseSource, EventResult e)
    {
        return new BatchStartEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            SqlText = e.GetString("batch_text")
        };
    }
}

internal class FileEventParser : IEventParser<FileEvent>
{
    public static FileEvent Map(DatabaseSource databaseSource, EventResult e)
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
}

internal class LatchEventParser : IEventParser<LatchEvent>
{
    public static LatchEvent Map(DatabaseSource databaseSource, EventResult e)
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
}

internal class QueryThreadParser : IEventParser<QueryThreadEvent>
{
    public static QueryThreadEvent Map(DatabaseSource databaseSource, EventResult e)
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
}

internal class WaitEventParser : IEventParser<WaitEvent>
{
    public static WaitEvent? Map(DatabaseSource databaseSource, EventResult e)
    {
        var waitType = (WaitType)(e.GetInt("wait_type") ?? 0);

        if (WaitEventFilter.CanIgnore(waitType.ToString()))
        {
            return null;
        }

        var isEnd = e.GetString("opcode") == "End";

        var waitResource = e.GetUlong("wait_resource");

        var duration = e.GetLong("duration") ?? 0;

        var waitEvent = new WaitEvent
        {
            Name = "Wait",
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            WaitType = waitType,
            IsEnd = isEnd,
            WaitResource = waitResource,
            DurationUs = duration,
        };

        return waitEvent;
    }
}

public class IoEventParser : IEventParser<IoEvent>
{
    public static IoEvent Map(DatabaseSource databaseSource, EventResult e)
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
}

internal class TransactionLogEventParser : IEventParser<TransactionLogEvent>
{
    public static TransactionLogEvent Map(DatabaseSource databaseSource, EventResult e)
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

internal class MemoryEventParser : IEventParser<MemoryEvent>
{
    public static MemoryEvent Map(DatabaseSource databaseSource, EventResult e)
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
}

internal class TransactionEventParser : IEventParser<TransactionEvent>
{
    public static TransactionEvent Map(DatabaseSource databaseSource, EventResult e)
    {
        return new TransactionEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            TransactionId = e.GetLong("transaction_id") ?? 0,
            State = (TransactionState)(e.GetInt("transaction_state") ?? 0),
        };
    }
}