using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.Events;

public sealed class EventReader(ILogger<EventReader> logger)
{
    public ILogger<EventReader> Logger { get; } = logger;

    public async Task<(List<EngineEvent>, List<ExecutionPlan>)> GetEvents(string filePath,
                                                                          string connectionString,
                                                                          DatabaseSource? database,
                                                                          Func<EngineEvent, bool>? endMarker = null)
    {
        await using var connection = new SqlConnection(connectionString);

        var events = new List<EngineEvent>();

        var executionPlans = new List<ExecutionPlan>();

        var resultsSql = GetResultsSql(filePath);

        await connection.OpenAsync();

        DateTime? startTimeStamp = null;

        await using (var reader = await new SqlCommand(resultsSql, connection).ExecuteReaderAsync())
        {
            Logger.LogDebug("SQL: {Sql}", resultsSql);

            var sequenceId = 0;

            while (await reader.ReadAsync())
            {
                var eventName = reader.GetString(0);
                var xml = reader.GetString(1);

                if (eventName == "query_post_execution_showplan")
                {
                    var plan = ExecutionPlanParser.Parse(xml);

                    executionPlans.Add(plan);
                }
                else
                {
                    var engineEvent = EventParser.ParseEvent(xml, database);

                    if (engineEvent is not null)
                    {
                        if (startTimeStamp is null)
                        {
                            startTimeStamp = engineEvent.Timestamp;
                        }

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

        var orderedEvents = events.OrderBy(e => e.Timestamp).ThenBy(e => e.SequenceId).ToList();

        // Captured timestamps are only millisecond-resolution, so a burst of page reads all land on the
        // same microsecond. Nudge each read within its bucket so they get distinct times.
        NudgeReads(orderedEvents);

        // Match Events to Execution Plan nodes, assigning PlanNodeIdentifier
        EventPlanNodeMatcher.Match(orderedEvents, executionPlans);

        // Build the operator events (timeline bars) bottom-up from each plan and its matched events.
        var operatorEvents = executionPlans
            .SelectMany(plan => new OperatorEventBuilder(plan, orderedEvents).Build())
            .ToList();

        orderedEvents.AddRange(operatorEvents);

        return (orderedEvents, executionPlans);
    }

    // The resolution window (microseconds) a coarse timestamp represents: a read logged at t happened
    // somewhere in [t, t + ReadWindowUs).
    private const long ReadWindowUs = 1000;

    /// <summary>
    /// Spreads page reads that share a coarse (millisecond-resolution) timestamp across their resolution
    /// window, so each read gets a distinct time and is individually visible/selectable on the timeline.
    /// Each read is offset by <c>ReadWindowUs / readCount * index</c> within its bucket.
    /// </summary>
    private static void NudgeReads(List<EngineEvent> events)
    {
        var i = 0;

        while (i < events.Count)
        {
            var bucketTime = events[i].TimeUs;

            var j = i;
            while (j < events.Count && events[j].TimeUs == bucketTime)
            {
                j++;
            }

            // The page reads sharing this timestamp.
            var readCount = 0;
            for (var k = i; k < j; k++)
            {
                if (events[k] is IoEvent { IsRead: true })
                {
                    readCount++;
                }
            }

            if (readCount > 1)
            {
                var index = 0;
                for (var k = i; k < j; k++)
                {
                    if (events[k] is IoEvent { IsRead: true })
                    {
                        events[k].TimeUs = bucketTime + ReadWindowUs * index / readCount;
                        index++;
                    }
                }
            }

            i = j;
        }
    }

    private string GetResultsSql(string filename)
    {
        return $@"
    SELECT object_name AS event_name, event_data
    FROM sys.fn_xe_file_target_read_file(
        '{filename.Replace(".xel", "")}*.xel',
        NULL, NULL, NULL
    );";
    }
}
