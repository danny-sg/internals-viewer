using System.Data;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.EventTypes;
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

        var consolidatedEvents = ReadGrouping.Group(collapsedEvents);

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

    // Smallest gap kept between two events so they never render touching ("adjacent"); also the overflow spacing.
    private const long MinGapUs = 40;

    /// <summary>
    /// Spreads the millisecond-bucketed events across their window using the microsecond durations, per lane
    /// </summary>
    /// <remarks>
    /// Each lane (event type — reads, latches, waits...) is a separate visual row, so overlap only matters within a
    /// lane; every lane is spread independently. Within a lane the query is serial (task_address is constant, so there
    /// is one stream), so events are ordered by their bucket and, for the events sharing a bucket, distributed across
    /// [bucket, bucket + 1000us]: each event is centred in its share of the window — <c>start = slotStart +
    /// (slot - duration) / 2</c> — which keeps a gap on both sides so they are neither overlapping nor adjacent.
    ///
    /// When a lane's events do not fit (a read whose duration overruns its slot, or a busy bucket) a running cursor
    /// pushes each following event out past the previous one's end plus a minimum gap, overflowing into later buckets
    /// rather than overlapping. Envelope events (per-thread profile totals, memory grants) are left alone as their
    /// durations span, rather than sit within, the stream.
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
        // Earliest start the next event may take so it neither precedes its bucket nor touches the previous event.
        var cursor = long.MinValue;

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

            var windowStart = Math.Max(bucket, cursor);

            // The window runs to the next bucket; a previous overflow can eat into it, so the slot is floored at 1.
            var slot = Math.Max(1, (bucket + BucketUs - windowStart) / count);

            for (var k = 0; k < count; k++)
            {
                var e = lane[i + k];

                // Centre the event in its slot, then hold it at or after both the window start (so it never precedes
                // its own bucket) and the cursor (so it can never overlap or touch the one before) — an over-long
                // event just starts on its boundary and pushes the rest out.
                var centred = windowStart + k * slot + (slot - e.DurationUs) / 2;

                var start = Math.Max(centred, Math.Max(cursor, windowStart));

                ShiftTo(e, start);

                cursor = start + e.DurationUs + MinGapUs;
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
    private static bool IsSerialWork(EngineEvent e) => e is not (QueryThreadEvent or MemoryEvent);

    private static string GetResultsSql(string filename)
    {
        return $@"
    SELECT object_name AS event_name, event_data
    FROM sys.fn_xe_file_target_read_file(
        '{filename.Replace(".xel", "")}*.xel',
        NULL, NULL, NULL
    );";
    }
}

public readonly record struct XEventPayload(string Name, string Value)
{
    public override string ToString() => Value;
}