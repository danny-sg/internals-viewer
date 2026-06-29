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

        // Captured timestamps are only millisecond-resolution, so a burst of events all land on the same
        // microsecond. Spread each bucket of coincident events across its window so they get distinct,
        // individually addressable times (in capture/sequence order).
        SpreadCoincidentEvents(orderedEvents);

        // Match Events to Execution Plan nodes, assigning PlanNodeIdentifier
        EventPlanNodeMatcher.Match(orderedEvents, executionPlans);

        // Build the operator events (timeline bars) bottom-up from each plan and its matched events.
        var operatorEvents = executionPlans
            .SelectMany(plan => new OperatorEventBuilder(plan, orderedEvents).Build())
            .ToList();

        orderedEvents.AddRange(operatorEvents);

        return (orderedEvents, executionPlans);
    }

    // The resolution window (microseconds) a coarse timestamp represents: an event logged at t happened
    // somewhere in [t, t + ResolutionWindowUs).
    private const long ResolutionWindowUs = 1000;

    /// <summary>
    /// Spreads each bucket of events that share a coarse (millisecond-resolution) timestamp evenly across
    /// its resolution window, in the existing (timestamp, sequence-id) order, so each event gets a
    /// distinct, individually addressable time. The k-th of <c>n</c> coincident events is offset by
    /// <c>ResolutionWindowUs * k / n</c>, so they stay within the window and keep their capture order.
    /// </summary>
    private static void SpreadCoincidentEvents(List<EngineEvent> events)
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

            var count = j - i;

            if (count > 1)
            {
                for (var k = 0; k < count; k++)
                {
                    events[i + k].TimeUs = bucketTime + ResolutionWindowUs * k / count;
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
