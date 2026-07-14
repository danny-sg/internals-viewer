using System.Data;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Parsers;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Plans;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.Events;

public sealed class EventReader(ILogger<EventReader> logger)
{
    public ILogger<EventReader> Logger { get; } = logger;

    public async Task<(List<EngineEvent>, List<ExecutionPlan>, CallStackTree)> GetEvents(string filePath,
                                                                                         string connectionString,
                                                                                         DatabaseSource? database,
                                                                                         bool includeSystemObjects,
                                                                                         CancellationToken cancellationToken,
                                                                                         Func<EngineEvent, bool>? endMarker = null)
    {
        await using var connection = new SqlConnection(connectionString);

        var events = new List<EngineEvent>();

        var executionPlans = new List<ExecutionPlan>();

        // Map plan handles to PlanHandleId
        var planHandles = new PlanHandleRegistry();

        var resultsSql = GetResultsSql(filePath);

        await connection.OpenAsync(cancellationToken);

        DateTime? startTimeStamp = null;

        var eventParser = new XmlEventParser(database, planHandles, new EventParser());

        // Use buffer for XML/Name to prevent repeated string allocations for each row
        var xmlBuffer = new char[4096];

        var nameBuffer = new char[64];

        // SequentialAccess required to read GetChars directly into buffer
        await using (var reader =
                     await new SqlCommand(resultsSql, connection)
                            .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken))
        {
            Logger.LogDebug("SQL: {Sql}", resultsSql);

            var sequenceId = 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                var nameLength = ReadColumn(reader, 0, ref nameBuffer);

                if (nameBuffer.AsSpan(0, nameLength) is "query_post_execution_showplan")
                {
                    var plan = ExecutionPlanParser.Parse(reader.GetString(1), planHandles);

                    executionPlans.Add(plan);
                }
                else
                {
                    // Stream the event_data column into buffer
                    var length = ReadColumn(reader, 1, ref xmlBuffer);

                    if (Logger.IsEnabled(LogLevel.Trace))
                    {
                        var xml = new string(xmlBuffer, 0, length);
                        var eventName = new string(nameBuffer, 0, nameLength);

                        Logger.LogTrace("XE Event:{Event}",
                                        new XEventPayload(eventName, xml));
                    }

                    var engineEvent = eventParser.ParseEvent(xmlBuffer, length);

                    if (engineEvent is not null)
                    {
                        if (!includeSystemObjects && IsSystemObjectEvent(engineEvent))
                        {
                            continue;
                        }

                        startTimeStamp ??= engineEvent.Timestamp;

                        // Gaps in sequence ids to allow the plan nodes to be slotted in
                        engineEvent.SequenceId = sequenceId += 100;

                        engineEvent.TimeUs = (long)(engineEvent.Timestamp - startTimeStamp.Value).TotalMicroseconds;

                        if (endMarker is not null && endMarker(engineEvent))
                        {
                            break;
                        }

                        events.Add(engineEvent);
                    }
                }
            }
        }

        connection.Close();

        var orderedEvents = events.OrderBy(e => e.SequenceId).ToList();

        // Consolidate: fold Begin/End pairs into single events, then bind the storage events of each page
        // read into a single NonCachedReadEventGroup.
        var collapsedEvents = IntervalCollapser.Collapse(orderedEvents);

        // Give never-released lock acquires (fine-grained locks dropped by escalation, or held to a commit outside the
        // window) their held duration, so they render as held bars rather than zero-duration points.
        // HeldLockCloser.Close(collapsedEvents);

        // Collapse a burst of identical buffer latches on one page (a scan re-latching a page per row) into one, so
        // the read grouping forms a single buffer-pool read per page visit and the spread can't smear them.
        collapsedEvents = BufferLatchCoalescing.Coalesce(collapsedEvents);


        await GetEventKeyAddresses(collapsedEvents, connectionString, cancellationToken);

        var consolidatedEvents = ReadGrouping.Group(collapsedEvents);

        // Bind each transaction's locks on one object into a single LockGroup (the escalation chain).
        consolidatedEvents = LockGrouping.Group(consolidatedEvents);

        // Recover a serial-order timeline from the millisecond-resolution timestamps by laying each worker's events
        // end-to-end using their microsecond durations, so a read overrunning its bucket pushes the next one out.
        SpreadEvents(consolidatedEvents);

        // Match Events to Execution Plan nodes, assigning PlanNodeIdentifier
        EventPlanNodeMatcher.Match(consolidatedEvents, executionPlans);

        // Build the operator events (timeline bars) bottom-up from each plan and its matched events.
        var operatorEvents = executionPlans.SelectMany(plan => new OperatorEventBuilder(plan, consolidatedEvents).Build())
                                           .ToList();

        consolidatedEvents.AddRange(operatorEvents);

        return (consolidatedEvents, executionPlans, eventParser.CallStack);
    }

    // System-object events hidden unless IncludeSystemObjects is on: allocation-unit events on a system object, and the
    // metadata/database-scoped locks (engine bookkeeping, resource database_id = 1). Those locks carry no allocation
    // unit, so the IsSystem check alone can't catch them — they must be matched by resource type.
    private static bool IsSystemObjectEvent(EngineEvent engineEvent) =>
        engineEvent.AllocationUnit?.IsSystem == true
        || engineEvent is LockEvent { Resource.ResourceType: LockResourceType.Metadata or LockResourceType.Database };

    private static int ReadColumn(SqlDataReader reader, int ordinal, ref char[] buffer)
    {
        var total = 0;

        while (true)
        {
            if (total == buffer.Length)
            {
                Array.Resize(ref buffer, buffer.Length * 2);
            }

            var read = (int)reader.GetChars(ordinal, total, buffer, total, buffer.Length - total);

            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    // Timestamps resolve only to the millisecond (a true time is within +/-500us of its bucket); durations are the
    // microsecond-accurate signal. Events sharing a bucket are spread across it rather than left stacked.
    private const long BucketUs = 1_000;

    /// <summary>
    /// Spreads the millisecond-bucketed events across their own window, per lane, by how many share the window
    /// </summary>
    /// <remarks>
    /// Each lane (event type — reads, latches, waits...) is a separate visual row, so overlap only matters within a
    /// lane; every lane is spread independently. Events are ms-quantized, so many land on the same millisecond. Within a
    /// lane the events sharing a bucket are distributed across [bucket, bucket + 1000us], each CENTRED in its 1/count
    /// slice — density is the COUNT sharing the window: a busy bucket simply packs tighter (thinner slices, eventually
    /// overlapping) rather than pushing later events forward.
    ///
    /// Critically the layout is CONFINED to each event's own bucket — it does NOT carry a cursor across buckets. Laying
    /// events end-to-end by duration (the old model) let a dense run overrun the real elapsed time and drag the read
    /// lane out past the operator/lock it belongs to; keeping each within its captured millisecond avoids that, at the
    /// cost of letting a genuinely over-full bucket overlap. Envelope events (per-thread profile totals, memory grants)
    /// are left alone as their durations span, rather than sit within, the stream.
    /// </remarks>
    internal static void SpreadEvents(List<EngineEvent> events)
    {
        foreach (var lane in events.Where(IsSerialWork).GroupBy(e => e.GetType()))
        {
            SpreadLane([.. lane.OrderBy(e => e.TimeUs).ThenBy(e => e.SequenceId)]);
        }
    }

    private static void SpreadLane(List<EngineEvent> lane)
    {
        var i = 0;

        while (i < lane.Count)
        {
            var bucket = lane[i].TimeUs / BucketUs * BucketUs;

            var j = i;

            while (j < lane.Count && lane[j].TimeUs / BucketUs * BucketUs == bucket)
            {
                j++;
            }

            var count = j - i;

            // Density = how many events share this millisecond: each gets an equal 1/count slice of the window.
            var slot = Math.Max(1, BucketUs / count);

            for (var k = 0; k < count; k++)
            {
                var e = lane[i + k];

                // Centre the event in its slice; never let it precede its own bucket. Confined to the bucket — a busy
                // bucket packs tighter (and, when truly over-full, overlaps) instead of overflowing into later ones.
                var centred = bucket + k * slot + (slot - e.DurationUs) / 2;

                ShiftTo(e, Math.Max(bucket, centred));
            }

            i = j;
        }
    }

    // Moves an event to its spread start; a grouped read carries its members with it (by the same delta) so its child
    // events stay aligned under it. The group's spread position is the corrected timeline — the members' own timestamps
    // are the ms-quantised ones the spread exists to replace — so without this the members would show earlier than the
    // group they belong to.
    private static void ShiftTo(EngineEvent e, long start)
    {
        var delta = start - e.TimeUs;

        e.TimeUs = start;

        if (delta != 0 && e is ReadEventGroup group)
        {
            foreach (var member in group.Events)
            {
                member.TimeUs += delta;
            }
        }
    }

    // Events whose duration is elapsed serial work (so must not overlap the next in their lane), as opposed to the
    // envelope durations of query_thread_profile totals and memory grants that span the whole stream.
    // Locks are held CONCURRENTLY (a query holds many at once, each spanning acquire→release), so they are NOT serial
    // work — laying them end-to-end by their (now sizeable) hold durations would push later locks far past the query.
    // They keep their raw timestamps and overlap instead.
    private static bool IsSerialWork(EngineEvent e) => e is not (QueryThreadEvent or MemoryEvent or LockEvent or LockGroup);

    private static string GetResultsSql(string filename)
    {
        return $@"
    SELECT object_name AS event_name, event_data
    FROM sys.fn_xe_file_target_read_file(
        '{filename.Replace(".xel", "")}*.xel',
        NULL, NULL, NULL
    );";
    }

    internal async Task GetEventKeyAddresses(List<EngineEvent> events,
                                             string connectionString,
                                             CancellationToken cancellationToken)
    {
        var keyLockEvents = events.Where(e => e is LockEvent { Resource.KeyHash: not null }).Cast<LockEvent>();

        var byAllocationUnitId = keyLockEvents.GroupBy(g => g.AllocationUnit);

        foreach (var grouping in byAllocationUnitId)
        {
            var allocationUnit = grouping.Key;

            if (allocationUnit is null || allocationUnit.IsSystem)
            {
                continue;
            }

            Logger.LogDebug("Getting lock key hash values for {AllocationUnitName}", allocationUnit.DisplayName);

            var objectName = $"{allocationUnit.SchemaName}.{allocationUnit.TableName}";

            var hashes = grouping.Select(s => s.Resource.KeyHash ?? string.Empty)
                                 .Where(h => !string.IsNullOrEmpty(h))
                                 .Distinct()
                                 .ToList();

            Logger.LogDebug("- {Count} keys", hashes.Count);

            var keyHashRowIdentifiers = await KeyHashLookup.GetKeyHashRowIdentifiers(objectName,
                                                                                     hashes,
                                                                                     connectionString,
                                                                                     cancellationToken);

            Logger.LogDebug("- found {Count} key hash -> RID mappings", keyHashRowIdentifiers.Count);

            foreach (var lockEvent in grouping)
            {
                if (lockEvent.Resource.KeyHash is not null
                    && keyHashRowIdentifiers.TryGetValue(lockEvent.Resource.KeyHash,
                        out var rowIdentifier))
                {
                    lockEvent.Resource.RowIdentifier = rowIdentifier;
                }
            }

        }
    }
}

public readonly record struct XEventPayload(string Name, string Value)
{
    public override string ToString() => Value;
}