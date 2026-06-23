using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Replay.Events.EventTypes;
using InternalsViewer.Replay.Plans;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Replay.Events;

public class EventReader(ILogger<EventReader> logger)
{
    public ILogger<EventReader> Logger { get; } = logger;

    public async Task<(List<EngineEvent>, List<ExecutionPlan>)> GetEvents(string filePath,
                                                                          string connectionString,
                                                                          DatabaseSource? database)
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
                        engineEvent.SequenceId = sequenceId += 2;

                        engineEvent.TimeMs = (long)(engineEvent.Timestamp - startTimeStamp.Value).TotalMilliseconds;

                        events.Add(engineEvent);
                    }
                }
            }
        }

        connection.Close();

        var orderedEvents = events.OrderBy(e => e.SequenceId).ToList();

        // Match Events to Execution Plan nodes, assigning PlanNodeIdentifier
        EventPlanNodeMatcher.Match(orderedEvents, executionPlans);

        ExecutionPlanParser.MergePlanEvents(orderedEvents, executionPlans);

        return (orderedEvents, executionPlans);
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
