using System.Data;
using System.Diagnostics;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Parsers;
using InternalsViewer.Query.Parsing.Plans;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.Events;

public sealed class EventReader(ILogger<EventReader> logger)
{
    public ILogger<EventReader> Logger { get; } = logger;

    /// <summary>
    /// Gets events/execution plan/call stack from an extended events (.xel) file
    /// </summary>
    /// <remarks>
    /// Due to the potentially high volume of events that could be read the reader is optimized for minimal memory allocations
    /// </remarks>
    public async Task<(List<EngineEvent>, List<ExecutionPlan>, CallStackTree)> GetEvents(string filePath,
                                                                                         string connectionString,
                                                                                         DatabaseSource? database,
                                                                                         bool includeSystemObjects,
                                                                                         IProgress<string>? progress,
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

        var start = Stopwatch.GetTimestamp();

        // SequentialAccess required to read GetChars directly into buffer
        await using (var reader = await new SqlCommand(resultsSql, connection)
                                                .ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken))
        {
            Logger.LogDebug("SQL: {Sql}", resultsSql);

            var sequenceId = 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                var nameLength = ReadColumn(reader, 0, ref nameBuffer);

                if (nameBuffer.AsSpan(0, nameLength) is "query_post_execution_showplan")
                {
                    progress?.Report("Parsing query plan");

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

        Logger.LogDebug("Read {Count} events in {Duration}", events.Count, Stopwatch.GetElapsedTime(start));

        progress?.Report("Processing events");

        var consolidatedEvents = await PostProcessEvents(events, connectionString, progress, cancellationToken);

        progress?.Report("Matching events to execution plan");

        start = Stopwatch.GetTimestamp();

        // Match Events to Execution Plan nodes, assigning PlanNodeIdentifier
        EventPlanNodeMatcher.Match(consolidatedEvents, executionPlans);

        // Build the operator events bottom-up from each plan and its matched events
        var operatorEvents = executionPlans.SelectMany(plan => new OperatorEventBuilder(plan, consolidatedEvents).Build())
                                           .ToList();

        Logger.LogDebug("Matched events to execution plan in {Duration}", Stopwatch.GetElapsedTime(start));

        consolidatedEvents.AddRange(operatorEvents);

        return (consolidatedEvents, executionPlans, eventParser.CallStack);
    }

    /// <summary>
    /// Processes the events through several steps to link and structure events
    /// </summary>
    /// <remarks>
    /// Extended events from SQL Server have several problems that that post-processing tries to solve. The raw events are coming from
    /// different parts of the database engine, at different points of the query lifecycle with different levels of detail. This processing
    /// translates the raw data into something that is (hopefully) consistent.
    ///
    /// This includes:
    ///     - Event matching for begin/end events and inference where a sequence is not explicit
    /// 
    ///     - Time/Duration corrections and inference. Extended events only capture to the nearest 100us and duration is often missing or
    ///       needs to be pieced together from several events
    ///
    ///     - Grouping where multiple events are pieced together from their operation or target and patterns are identified to translate
    ///       multiple raw events into a single operation.
    /// </remarks>
    private async Task<List<EngineEvent>> PostProcessEvents(List<EngineEvent> events, 
                                                            string connectionString,
                                                            IProgress<string>? progress, 
                                                            CancellationToken cancellationToken)
    {
   

        var orderedEvents = events.OrderBy(e => e.SequenceId).ToList();

        var start = Stopwatch.GetTimestamp();

        var collapsedEvents = IntervalCollapser.Collapse(orderedEvents);

        Logger.LogDebug("Collapsed intervals in {Duration}", Stopwatch.GetElapsedTime(start));

        start = Stopwatch.GetTimestamp();

        HeldLockCloser.Close(collapsedEvents);

        Logger.LogDebug("Held locks closed in {Duration}", Stopwatch.GetElapsedTime(start));

        start = Stopwatch.GetTimestamp();

        collapsedEvents = LockPartitionCollapser.Collapse(collapsedEvents);

        Logger.LogDebug("Collapsed lock partitions in {Duration}", Stopwatch.GetElapsedTime(start));

        start = Stopwatch.GetTimestamp();

        collapsedEvents = BufferLatchCoalescing.Coalesce(collapsedEvents);

        Logger.LogDebug("Coalesced buffer latches in {Duration}", Stopwatch.GetElapsedTime(start));

        await GetEventKeyAddresses(collapsedEvents, connectionString, progress, cancellationToken);

        start = Stopwatch.GetTimestamp();

        var consolidatedEvents = ReaderGrouper.Group(collapsedEvents);

        Logger.LogDebug("Grouped reads in {Duration}", Stopwatch.GetElapsedTime(start));

        start = Stopwatch.GetTimestamp();

        consolidatedEvents = LockGrouper.Group(consolidatedEvents);

        Logger.LogDebug("Grouped locks in {Duration}", Stopwatch.GetElapsedTime(start));

        start = Stopwatch.GetTimestamp();

        EventSpreader.SpreadEvents(consolidatedEvents);

        Logger.LogDebug("Spread events in {Duration}", Stopwatch.GetElapsedTime(start));

        return consolidatedEvents;
    }

    private static bool IsSystemObjectEvent(EngineEvent engineEvent) =>
        engineEvent.AllocationUnit?.IsSystem == true
        || engineEvent is LockEvent { Resource.ResourceType: LockResourceType.Metadata or LockResourceType.Database };

    /// <summary>
    /// Read a column into the referenced buffer
    /// </summary>
    /// <remarks>
    /// Includes resizing of the buffer if necessary
    /// </remarks>
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
                                            IProgress<string>? progress,
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

            progress?.Report($"Getting lock key hash values for {allocationUnit.DisplayName}");

            var hashes = grouping.Select(s => s.Resource.KeyHash ?? string.Empty)
                                 .Where(h => !string.IsNullOrEmpty(h))
                                 .Distinct()
                                 .ToList();

            Logger.LogDebug("- {Count} keys", hashes.Count);

            var keyHashRowIdentifiers = await KeyHashLookup.GetKeyHashRowIdentifiers(allocationUnit.SchemaName,
                                                                                     allocationUnit.TableName,
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