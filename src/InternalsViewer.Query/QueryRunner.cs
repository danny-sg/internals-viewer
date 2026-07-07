using System.Diagnostics;
using System.Text;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.TransactionLog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query;

public sealed record EventOptions
{
    public bool IncludeLock { get; set; } = true;

    public bool IncludeWait { get; set; } = true;

    public bool IncludeCallstack { get; set; }
}
public sealed class QueryRunner(ILogger<QueryRunner> logger,
                                EventReader eventReader,
                                LogRecordReader logRecordReader)
{
    private ILogger<QueryRunner> Logger { get; } = logger;

    public EventReader EventReader { get; } = eventReader;

    private LogRecordReader LogRecordReader { get; } = logRecordReader;

    private readonly string[] _events =
    [
        "sqlserver.sql_batch_starting",
        "sqlserver.sql_batch_completed",
        "sqlserver.rpc_starting",
        "sqlserver.rpc_completed",
        "sqlserver.file_write_completed",
        "sqlserver.log_flush_complete",
        "sqlserver.page_split",
        "sqlserver.query_thread_profile",
        "sqlserver.physical_page_read",
        "sqlserver.physical_page_write",
        "sqlserver.query_post_execution_showplan",
        "sqlserver.query_memory_grant_usage"
    ];

    private readonly string[] _lockEvents =
    [
        "sqlserver.lock_acquired",
        "sqlserver.lock_released",
    ];

    private readonly string[] _waitEvents =
    [
        "sqlos.wait_info",
    ];

    private readonly string[] _logEvents =
    [
        "sqlserver.transaction_log"
    ];

    private readonly string[] _actions =
    [
        "sqlserver.session_id",
        "sqlserver.request_id",
        "sqlserver.sql_text",
        "sqlserver.database_id",
        "sqlserver.plan_handle",
        "sqlserver.transaction_id",
        "package0.event_sequence",

    ];

    private readonly string[] _callstackActions =
    [
        "package0.callstack"
    ];

    public async Task<QueryResult> TraceQuery(string sqlText,
                                              string connectionString,
                                              bool clearBufferPool,
                                              bool disableReadAhead,
                                              bool isModification,
                                              EventOptions eventOptions,
                                              IProgress<string>? progress,
                                              CancellationToken cancellationToken)
    {
        long rowCount;
        var sessionId = $"QueryReplay_{Guid.NewGuid():N}";

        List<EngineEvent>? events;
        List<ExecutionPlan>? executionPlans;

        Func<EngineEvent, bool>? endMarker = null;

        if (isModification)
        {
            endMarker = e =>
                e is BatchStartEvent batchStart &&
                batchStart.SqlText.Contains($"ROLLBACK TRANSACTION iv_{sessionId[..28]}");
        }

        try
        {
            (var filePath, rowCount, var logRecords) = await RunQueryWithEventSession(sessionId,
                                                                                      sqlText,
                                                                                      connectionString,
                                                                                      clearBufferPool,
                                                                                      disableReadAhead,
                                                                                      isModification,
                                                                                      eventOptions,
                                                                                      progress,
                                                                                      cancellationToken);

            (events, executionPlans) = await EventReader.GetEvents(filePath,
                                                                   connectionString,
                                                                   null,
                                                                   cancellationToken,
                                                                   endMarker);
            if (eventOptions.IncludeCallstack)
            {
                var symbolsPath = @"C:\Symbols";

                await CallstackProcessor.Process(events, symbolsPath, progress, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            progress?.Report("Query cancelled");

            return new QueryResult
            {
                IsSuccess = false,
                Message = "Query cancelled",
                SessionId = sessionId
            };
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
                                              bool clearBufferPool,
                                              bool disableReadAhead,
                                              bool isModification,
                                              EventOptions eventOptions,
                                              IProgress<string>? progress,
                                              CancellationToken cancellationToken)
    {
        var connectionString = database.Connection.GetConnectionString();

        var sessionId = $"QueryReplay_{Guid.NewGuid():N}";

        long rowCount;

        List<EngineEvent>? events;
        List<ExecutionPlan>? executionPlans;

        Func<EngineEvent, bool>? endMarker = null;

        if (isModification)
        {
            endMarker = e =>
                e is BatchStartEvent batchStart &&
                batchStart.SqlText.Contains($"ROLLBACK TRANSACTION iv_{sessionId[..28]}");
        }

        try
        {
            (var filePath, rowCount, var logRecords) = await RunQueryWithEventSession(sessionId,
                                                                                      sqlText,
                                                                                      connectionString,
                                                                                      clearBufferPool,
                                                                                      disableReadAhead,
                                                                                      isModification,
                                                                                      eventOptions,
                                                                                      progress,
                                                                                      cancellationToken);

            var eventsStart = Stopwatch.GetTimestamp();

            (events, executionPlans) = await EventReader.GetEvents(filePath,
                                                                   connectionString,
                                                                   database,
                                                                   cancellationToken,
                                                                   endMarker);

            progress?.Report($"{events.Count} event(s) retrieved in {Stopwatch.GetElapsedTime(eventsStart)}");

            if (eventOptions.IncludeCallstack)
            {
                progress?.Report($"Processing callstack frames");

                var symbolsPath = @"C:\Symbols";

                await CallstackProcessor.Process(events, symbolsPath, progress, cancellationToken);
            }

            await GetEventKeyAddresses(events, database.AllocationUnits, connectionString, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new QueryResult
            {
                IsSuccess = false,
                Message = "Query cancelled",
                SessionId = sessionId
            };
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

    private static async Task GetEventKeyAddresses(List<EngineEvent> events,
                                                   Dictionary<long, AllocationUnit> allocationUnits,
                                                   string connectionString,
                                                   CancellationToken cancellationToken)
    {
        var keyLockEvents = events.Where(e => e is LockEvent { KeyHash: not null }).Cast<LockEvent>();

        var keyLockEventsByObjectId = keyLockEvents.GroupBy(g => g.ObjectId);

        foreach (var grouping in keyLockEventsByObjectId)
        {
            var objectId = grouping.Key;

            var allocationUnit = allocationUnits.Values.FirstOrDefault(f => f.ObjectId == objectId);

            if (allocationUnit is null)
            {
                continue;
            }

            var objectName = $"{allocationUnit.SchemaName}.{allocationUnit.TableName}";

            var hashes = grouping.Select(s => s.KeyHash ?? string.Empty).Where(h => !string.IsNullOrEmpty(h)).ToList();

            var keyHashRowIdentifiers = await KeyHashLookup.GetKeyHashRowIdentifiers(objectName,
                                                                                     hashes,
                                                                                     connectionString,
                                                                                     cancellationToken);

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



    private async Task<(string, long, List<LogRecord> logRecords)>
        RunQueryWithEventSession(string sessionName,
                                 string sqlText,
                                 string connectionString,
                                 bool clearBufferPool,
                                 bool disableReadAhead,
                                 bool isReplayMode,
                                 EventOptions eventOptions,
                                 IProgress<string>? progress,
                                 CancellationToken cancellationToken)
    {
        long rowCount = 0;

        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        var logPath = (string?)await new SqlCommand(GetFileLocationSql(), connection)
                                            .ExecuteScalarAsync(cancellationToken);

        var filePath = $"{logPath}\\{sessionName}.xel";

        List<LogRecord> logRecords = [];
        string? startLsn = null;

        var spid = await ExecuteScalar<short>("SELECT @@SPID", connection);

        var createSessionSql = GetCreateSessionSql(sessionName, filePath, spid, isReplayMode, eventOptions);

        await ExecuteSql(createSessionSql, connection, cancellationToken);

        if (clearBufferPool | isReplayMode)
        {
            // Flush dirty pages either for DROPCLEANBUFFERS or to write the transaction log to disk 
            await ExecuteSql("CHECKPOINT", connection, cancellationToken);
        }

        if (clearBufferPool)
        {
            // Removes all pages from the buffer pool so pages will come from I/O rather than the cache
            await ExecuteSql("DBCC DROPCLEANBUFFERS", connection, cancellationToken);
        }

        if (disableReadAhead)
        {
            // Disable pre-fetching page scans for the session
            await ExecuteSql("DBCC TRACEON(652)", connection, cancellationToken);
        }

        if (isReplayMode)
        {
            startLsn = await ExecuteScalar<string?>(
                "SELECT MAX([Current LSN]) FROM fn_dblog(NULL, NULL);", connection);

            progress?.Report($"Start LSN: {startLsn}");
        }

        // Session block that should stop the session if there is any failure
        try
        {
            await ExecuteSql(GetStartSessionSql(sessionName), connection, cancellationToken);

            if (isReplayMode)
            {
                progress?.Report($"Transaction started");

                await ExecuteSql($"BEGIN TRANSACTION iv_{sessionName[..28]};", connection, cancellationToken);
            }

            await Task.Delay(100, cancellationToken);

            Logger.LogDebug("SQL: {Sql}", sqlText);

            var queryStart = Stopwatch.GetTimestamp();

            await using var reader = await new SqlCommand(sqlText, connection).ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rowCount++;
            }

            await reader.CloseAsync();

            progress?.Report($"Query executed in: {Stopwatch.GetElapsedTime(queryStart)}");

            if (isReplayMode)
            {
                logRecords = await LogRecordReader.GetLogRecords(connection, startLsn, sessionName);

                progress?.Report($"{logRecords.Count} log record(s) retrieved");

                await ExecuteSql($"ROLLBACK TRANSACTION iv_{sessionName[..28]};", connection, cancellationToken);

                progress?.Report($"Transaction rolled back");
            }
        }
        finally
        {
            // Cleanup must run even when the query was cancelled, so it must not observe the (now cancelled)
            // token - otherwise the Extended Events session is left running on the server.
            try
            {
                await ExecuteSql(GetStopSessionSql(sessionName), connection, CancellationToken.None);
            }
            catch
            {
                // No-op
            }

            try
            {
                await ExecuteSql(GetDropSessionSql(sessionName), connection, CancellationToken.None);
            }
            catch
            {
                // No-op
            }
        }

        return (filePath, rowCount, logRecords);
    }

    private static async Task<T?> ExecuteScalar<T>(string sql, SqlConnection connection)
    {
        var result = await new SqlCommand(sql, connection).ExecuteScalarAsync();

        return (T?)result;
    }

    private async Task ExecuteSql(string sql, SqlConnection connection, CancellationToken cancellationToken)
    {
        Logger.LogDebug("SQL: {Sql}", sql);

        await new SqlCommand(sql, connection).ExecuteNonQueryAsync(cancellationToken);
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

    private string GetCreateSessionSql(string sessionName,
                                       string filePath,
                                       short spid,
                                       bool isReplayMode,
                                       EventOptions eventOptions)
    {

        var sessionEvents = new List<string>(_events);
        var sessionActions = new List<string>(_actions);

        if (isReplayMode)
        {
            sessionEvents.AddRange(_logEvents);
        }

        if (eventOptions.IncludeLock)
        {
            sessionEvents.AddRange(_lockEvents);
        }

        if (eventOptions.IncludeWait)
        {
            sessionEvents.AddRange(_waitEvents);
        }

        if (eventOptions.IncludeCallstack)
        {
            sessionActions.AddRange(_callstackActions);
        }

        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"CREATE EVENT SESSION [{sessionName}] ON SERVER");

        for (var i = 0; i < sessionEvents.Count; i++)
        {
            var eventName = sessionEvents[i];

            stringBuilder.Append($"ADD EVENT {eventName}");

            if (_actions.Length > 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append("(\n    ACTION (");

                stringBuilder.Append(string.Join(", ", sessionActions));

                stringBuilder.Append(")\n");
                stringBuilder.Append("    WHERE (");

                stringBuilder.Append($"sqlserver.session_id = {spid}");
                stringBuilder.Append($" AND sqlserver.sql_text NOT LIKE '%LOG_READ_{sessionName}%'");

                stringBuilder.Append(")");

                stringBuilder.Append("\n)");
            }

            if (i < sessionEvents.Count - 1)
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