using System.Data;
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

        var eventParser = new EventParser(database, planHandles);

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

                    var engineEvent = eventParser.ParseEvent(xmlBuffer.AsSpan(0, length));

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

        var orderedEvents = events.OrderBy(e => e.Timestamp).ThenBy(e => e.SequenceId).ToList();

        // Spread events for ms to us resolution
        SpreadCoincidentEvents(orderedEvents);

        // Match Events to Execution Plan nodes, assigning PlanNodeIdentifier
        EventPlanNodeMatcher.Match(orderedEvents, executionPlans);

        // Build the operator events (timeline bars) bottom-up from each plan and its matched events.
        var operatorEvents = executionPlans.SelectMany(plan => new OperatorEventBuilder(plan, orderedEvents).Build())
                                           .ToList();

        orderedEvents.AddRange(operatorEvents);

        return (orderedEvents, executionPlans);
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

    private const long ResolutionWindowUs = 1000;

    /// <summary>
    /// Spreads events over 1ms timestamp window
    /// </summary>
    /// <remarks>
    /// Spreads each bucket of events that share a coarse (millisecond-resolution) timestamp evenly across its
    /// resolution window, in the existing (timestamp, sequence-id) order, so each event gets a distinct, individually
    /// addressable time. The k-th of n coincident events is offset by ResolutionWindowUs * k / n, so they stay within
    /// the window and keep their capture order.
    /// </remarks>
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
