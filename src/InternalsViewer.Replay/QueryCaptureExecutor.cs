using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Replay.Events;
using InternalsViewer.Replay.Plans;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Text;

namespace InternalsViewer.Replay;

public sealed class QueryCaptureExecutor(ILogger<QueryCaptureExecutor> logger)
{
    public ILogger<QueryCaptureExecutor> Logger { get; } = logger;

    private readonly string[] _events =
    [
        "sqlserver.sql_batch_starting",
        "sqlserver.sql_batch_completed",
        "sqlserver.rpc_starting",
        "sqlserver.rpc_completed",
        "sqlserver.file_read",
        "sqlserver.file_write_completed",
        "sqlserver.log_flush_complete",
        "sqlserver.page_split",
        "sqlserver.lock_acquired",
        "sqlserver.lock_released",
        "sqlos.wait_info",
        "sqlserver.query_thread_profile",
        "sqlserver.physical_page_read",
        "sqlserver.physical_page_write",
        "sqlserver.query_post_execution_showplan"
    ];

    private readonly string[] _actions =
    [
        "sqlserver.session_id",
        "sqlserver.request_id",
        "sqlserver.sql_text",
        "sqlserver.database_id",
        "sqlserver.plan_handle"
    ];

    public async Task<QueryResult> TraceQuery(string sqlText,
                                              string connectionString,
                                              bool clearBufferPool)
    {
        long rowCount = 0;
        var sessionId = $"QueryReplay_{Guid.NewGuid():N}";

        List<EngineEvent>? events;
        List<ExecutionPlan>? executionPlans;

        try
        {
            (var filePath, rowCount) =
                await RunQueryWithEventSession(sessionId, sqlText, connectionString, clearBufferPool);

            (events, executionPlans) = await ParseResults(filePath, connectionString);
        }
        catch (SqlException ex)
        {
            var message = $"Msg: {ex.Number}, Level: {ex.Class}, State: {ex.State}, Line: {ex.LineNumber}"
                          + $"{Environment.NewLine}{ex.Message}";

            return new QueryResult
            {
                IsSuccess = false,
                Message = message,
                SessionId = sessionId
            };
        }
        catch (Exception ex)
        {
            var message = "Non-Database Error:"
                          + $"{Environment.NewLine}{ex.InnerException?.Message ?? ex.Message}"
                          + $"{Environment.NewLine}{ex.StackTrace}";

            return new QueryResult
            {
                IsSuccess = false,
                Message = message,
                SessionId = sessionId
            };
        }

        return new QueryResult
        {
            IsSuccess = true,
            EngineEvents = events,
            ExecutionPlans = executionPlans,
            SessionId = sessionId,
            RowCount = rowCount
        };
    }

    public async Task<QueryResult> TraceQuery(string sqlText,
                                              DatabaseSource database,
                                              bool clearBufferPool)
    {
        var connectionString = database.Connection.GetConnectionString();

        var sessionId = $"QueryReplay_{Guid.NewGuid():N}";

        long rowCount = 0;
        List<EngineEvent>? events;
        List<ExecutionPlan>? executionPlans;

        try
        {
            (var filePath, rowCount) = await RunQueryWithEventSession(sessionId, 
                                                                      sqlText, 
                                                                      connectionString, 
                                                                      clearBufferPool);

            (events, executionPlans) = await ParseResults(filePath, database);

            await GetEventKeyAddresses(events, database.AllocationUnits, connectionString);
        }
        catch (SqlException ex)
        {
            var message = $"Msg: {ex.Number}, Level: {ex.Class}, State: {ex.State}, Line: {ex.LineNumber}"
                          + $"{Environment.NewLine}{ex.Message}";

            return new QueryResult
            {
                IsSuccess = false,
                Message = message,
                SessionId = sessionId
            };
        }
        catch (Exception ex)
        {
            var message = "Non-Database Error:"
                          + $"{Environment.NewLine}{ex.InnerException?.Message ?? ex.Message}"
                          + $"{Environment.NewLine}{ex.StackTrace}";

            return new QueryResult
            {
                IsSuccess = false,
                Message = message,
                SessionId = sessionId
            };
        }

        return new QueryResult
        {
            IsSuccess = true,
            EngineEvents = events,
            ExecutionPlans = executionPlans,
            SessionId = sessionId,
            RowCount = rowCount
        };
    }

    private async Task GetEventKeyAddresses(List<EngineEvent> events,
                                            List<AllocationUnit> allocationUnits,
                                            string connectionString)
    {
        var keyLockEvents = events.Where(e => e is LockEvent { KeyHash: not null }).Cast<LockEvent>();

        var keyLockEventsByObjectId = keyLockEvents.GroupBy(g => g.ObjectId);

        foreach (var grouping in keyLockEventsByObjectId)
        {
            var objectId = grouping.Key;

            var allocationUnit = allocationUnits.FirstOrDefault(f => f.ObjectId == objectId);

            if (allocationUnit is null)
            {
                continue;
            }

            var objectName = $"{allocationUnit.SchemaName}.{allocationUnit.TableName}";

            var hashes = grouping.Select(s => s.KeyHash ?? string.Empty).Where(h => !string.IsNullOrEmpty(h)).ToList();

            var keyHashRowIdentifiers = await KeyHashLookup.GetKeyHashRowIdentifiers(objectName,
                                                                                     hashes,
                                                                                     connectionString);

            foreach (var lockEvent in grouping)
            {
                if (lockEvent.KeyHash is not null 
                    && keyHashRowIdentifiers.TryGetValue(lockEvent.KeyHash, 
                                                         out var rowIdentifier))
                {
                    lockEvent.RowIdentifier = rowIdentifier;
                }
            }
        }
    }

    private async Task<(List<EngineEvent>, List<ExecutionPlan>)> ParseResults(string filePath, string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);

        var events = new List<EngineEvent>();
        var executionPlans = new List<ExecutionPlan>();

        var resultsSql = GetResultsSql(filePath);

        await connection.OpenAsync();

        var sequenceId = 0;

        await using (var reader = await new SqlCommand(resultsSql, connection).ExecuteReaderAsync())
        {
            Logger.LogDebug("SQL: {Sql}", resultsSql);

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
                    var engineEvent = EventParser.ParseEvent(xml);

                    if (engineEvent is not null)
                    {
                        engineEvent.SequenceId = sequenceId++;
                        events.Add(engineEvent);
                    }
                }
            }
        }

        connection.Close();

        EventParser.SetRelativeTimestamps(events);

        var orderedEvents = events.OrderBy(e => e.SequenceId).ToList();

        PlanNodeMatcher.Match(orderedEvents, executionPlans);

        return (orderedEvents, executionPlans);
    }

    private async Task<(List<EngineEvent>, List<ExecutionPlan>)> ParseResults(string filePath, DatabaseSource database)
    {
        var connectionString = database.Connection.GetConnectionString();

        await using var connection = new SqlConnection(connectionString);

        var events = new List<EngineEvent>();

        var executionPlans = new List<ExecutionPlan>();

        var resultsSql = GetResultsSql(filePath);

        await connection.OpenAsync();

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
                        engineEvent.SequenceId = sequenceId++;

                        events.Add(engineEvent);
                    }
                }
            }
        }

        connection.Close();

        EventParser.SetRelativeTimestamps(events);

        var orderedEvents = events.OrderBy(e => e.SequenceId).ToList();

        PlanNodeMatcher.Match(orderedEvents, executionPlans);

        return (orderedEvents, executionPlans);
    }

    private async Task<(string, long)> RunQueryWithEventSession(string sessionName,
                                                        string sqlText,
                                                        string connectionString,
                                                        bool clearBufferPool)
    {
        long rowCount = 0;

        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync();

        var logPath = (string?)await new SqlCommand(GetFileLocationSql(), connection).ExecuteScalarAsync();

        var filePath = $"{logPath}\\{sessionName}.xel";

        await using var spidCommand = new SqlCommand("SELECT @@SPID", connection);

        var spid = (short)(await spidCommand.ExecuteScalarAsync() ?? 0);

        var createSessionSql = GetCreateSessionSql(sessionName, filePath, spid);

        await ExecuteSql(createSessionSql, connection);

        if (clearBufferPool)
        {
            await ExecuteSql("CHECKPOINT", connection);
            await ExecuteSql("DBCC DROPCLEANBUFFERS", connection);
        }

        try
        {
            await ExecuteSql(GetStartSessionSql(sessionName), connection);

            await Task.Delay(100);

            Logger.LogDebug("SQL: {Sql}", sqlText);

            await using (var reader = await new SqlCommand(sqlText, connection).ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    rowCount++;
                }
            }
        }
        finally
        {

            try
            {
                await ExecuteSql(GetStopSessionSql(sessionName), connection);
            }
            catch
            {
                // No-op
            }

            try
            {
                await ExecuteSql(GetDropSessionSql(sessionName), connection);
            }
            catch
            {
                // No-op
            }
        }

        return (filePath, rowCount);
    }

    private async Task ExecuteSql(string sql, SqlConnection connection)
    {
        Logger.LogDebug("SQL: {Sql}", sql);

        await new SqlCommand(sql, connection).ExecuteNonQueryAsync();
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

    private string GetFileLocationSql()
    {
        return @"
                SELECT LEFT(
                    CAST(SERVERPROPERTY('ErrorLogFileName') AS NVARCHAR(4000)),
                    LEN(CAST(SERVERPROPERTY('ErrorLogFileName') AS NVARCHAR(4000)))
                    - CHARINDEX('\', REVERSE(CAST(SERVERPROPERTY('ErrorLogFileName') AS NVARCHAR(4000))))
            );";
    }

    private string GetDropSessionSql(string sessionName)
    {
        return $"DROP EVENT SESSION [{sessionName}] ON SERVER;";
    }

    private string GetStartSessionSql(string sessionName)
    {
        return $"ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = START;";
    }

    private string GetStopSessionSql(string sessionName)
    {
        return $"ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = STOP;";
    }

    private string GetCreateSessionSql(string sessionName, string filePath, short spid)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"CREATE EVENT SESSION [{sessionName}] ON SERVER");

        for (int i = 0; i < _events.Length; i++)
        {
            var eventName = _events[i];

            stringBuilder.Append($"ADD EVENT {eventName}");

            if (_actions.Length > 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append("(\n    ACTION (");

                stringBuilder.Append(string.Join(", ", _actions));

                stringBuilder.Append(")");
                stringBuilder.Append($"\n    WHERE (sqlserver.session_id = {spid})");
                stringBuilder.Append("\n)");
            }

            if (i < _events.Length - 1)
            {
                stringBuilder.AppendLine(",");
            }
            else
            {
                stringBuilder.AppendLine();
            }
        }


        stringBuilder.AppendLine($@"
ADD TARGET package0.event_file
(
    SET filename = '{filePath}',
        max_file_size = (100),
        max_rollover_files = (2)
);");


        return stringBuilder.ToString();
    }
}
