using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Batches;
using InternalsViewer.Query.Events.Files;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Parsers.Xml;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.Query.Events.Waits;
using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.Query.Events.Parsers;

public sealed class EventParser
{
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
        engineEvent.SequenceId = sequenceId * 10 ?? e.SequenceId;

        if (e.Actions.TryGetValue("plan_handle", out var planHandle) && planHandle.Length > 0)
        {
            engineEvent.PlanHandleId = planHandles.GetOrAdd(e.Buffer.AsSpan(planHandle.Offset, planHandle.Length));
        }

        if (database is null)
        {
            return engineEvent;
        }

        if (engineEvent is not LockEvent)
        {
            if (engineEvent is PageEngineEvent { ObjectId: 0, PageAddress: { } pageAddress } pageEvent)
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
        }

        engineEvent.Category = EventCategoryClassifier.GetCategory(engineEvent);

        if (e.TryGetActionSpan("callstack", out var callstack))
        {
            var frames = XmlCallStackParser.ParseCallStack(callstack, _frameStrings);

            if (frames.Count > 0)
            {
                engineEvent.CallStack = callStack.Add(frames, engineEvent);
            }
        }

        return engineEvent;
    }
}