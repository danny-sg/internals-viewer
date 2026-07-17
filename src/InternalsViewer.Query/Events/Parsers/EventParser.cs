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

    /// <summary>
    /// Whether events on system objects are mapped rather than excluded
    /// </summary>
    public bool IncludeSystemObjects { get; init; }

    /// <summary>
    /// The BUF latch addresses of the system-object events excluded so far
    /// </summary>
    /// <remarks>
    /// Kept for the length of the batch: a wait can only be recognised as system work once the latch it measures has been
    /// seen, and capture order is unreliable — a wait is frequently buffered ahead of its latch, so the reader sweeps for
    /// them once the batch is read rather than as it goes. See <see cref="IsExcludedSystemWait"/>.
    /// </remarks>
    private readonly HashSet<ulong> _systemLatchAddresses = [];

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
            "sql_batch_completed"
                => BatchEndEventParser.Map(database!, e),
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

        if (IsExcluded(engineEvent))
        {
            return null;
        }

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

    /// <summary>
    /// Whether the event is system-object work being excluded, remembering the latch address if so
    /// </summary>
    /// <remarks>
    /// An event is system work by the allocation unit its page belongs to, which is why the latch address is worth
    /// keeping: a wait carries no page, so it never resolves to an allocation unit and can never be recognised here.
    /// Excluding the preamble's latches without its waits strands them, leaving no suspend for
    /// <see cref="Consolidation.WaitAligner"/> or <see cref="Consolidation.ReaderGrouper"/> to find by address.
    /// </remarks>
    internal bool IsExcluded(EngineEvent engineEvent)
    {
        if (IncludeSystemObjects || !IsSystemObjectEvent(engineEvent))
        {
            return false;
        }

        if (engineEvent is LatchEvent { LatchAddress: { } latchAddress })
        {
            _systemLatchAddresses.Add(latchAddress);
        }

        return true;
    }

    /// <summary>
    /// Whether the event is a wait on a latch that was excluded as system work
    /// </summary>
    /// <remarks>
    /// A page IO wait's wait_resource IS the address of the BUF latch it suspended on, so the addresses collected while
    /// excluding those latches identify the stranded waits exactly.
    /// </remarks>
    public bool IsExcludedSystemWait(EngineEvent engineEvent) =>
        engineEvent is WaitEvent { WaitResource: { } resource } && _systemLatchAddresses.Contains(resource);

    internal static bool IsSystemObjectEvent(EngineEvent engineEvent) =>
        engineEvent.AllocationUnit?.IsSystem == true
        || engineEvent is LockEvent { Resource.ResourceType: LockResourceType.Metadata or LockResourceType.Database };
}