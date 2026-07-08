using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Callstack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.TransactionLog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query;

public sealed record QueryOptions
{
    public bool ClearBufferPool { get; set; } = true;

    public bool DisableReadAhead { get; set; } = true;
}

public sealed record ExecuteSqlPayload(string SqlText,
                                       QueryOptions QueryOptions,
                                       StatementType StatementType,
                                       TrackedSelectionRange? TrackedSelection);

public sealed record TrackedSelectionRange(int Start, int End);

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
        "sqlserver.query_post_execution_showplan"
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

    private readonly string[] _memoryEvents =
    [
        "sqlserver.query_memory_grant_usage",
        "sqlserver.hash_spill_details",
        "sqlserver.sort_warning",
        "sqlserver.memory_grant_updated_by_feedback"
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

    public async Task<QueryResult> TraceQuery(ExecuteSqlPayload payload,
                                              DatabaseSource database,
                                              EventOptions eventOptions,
                                              string symbolsPath,
                                              IProgress<string>? progress,
                                              CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(payload.SqlText))
        {
            return new QueryResult
            {
                IsSuccess = false,
                RowCount = 0,
                SessionId = "None",
                Message = "Empty query text"
            };
        }

        var connectionString = database.Connection.GetConnectionString();

        var sessionId = $"QueryReplay_{Guid.NewGuid():N}";

        long rowCount;

        List<EngineEvent>? events;
        List<ExecutionPlan>? executionPlans;

        Func<EngineEvent, bool>? endMarker = null;

        var isReplayMode = false;

        var (preCommands, commands, postCommands) = PayloadParser.Parse(payload);

        if (commands.Length != 1
            || (payload.TrackedSelection == null
                && payload.StatementType is StatementType.MultiStatementSelect
                                            or StatementType.MultiStatementModification))
        {
            return new QueryResult
            {
                IsSuccess = false,
                RowCount = 0,
                SessionId = "None",
                Message = "Multi-statement queries cannot be traced. Select a single statement then right click and " +
                          "choose 'Trace query selection'."
            };
        }

        if (payload.StatementType == StatementType.Modification)
        {
            endMarker = e =>
                e is BatchStartEvent batchStart &&
                batchStart.SqlText.Contains($"ROLLBACK TRANSACTION iv_{sessionId[..28]}");

            isReplayMode = true;
        }

        try
        {
            (var filePath, rowCount, var logRecords) = await RunQueryWithEventSession(sessionId,
                                                                                      preCommands,
                                                                                      commands[0],
                                                                                      postCommands,
                                                                                      connectionString,
                                                                                      isReplayMode,
                                                                                      payload.QueryOptions,
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

            if (eventOptions.IncludeCallStack)
            {
                progress?.Report($"Processing callstack frames");

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
                                 string[] preCommandSql,
                                 string commandSql,
                                 string[] postCommandSql,
                                 string connectionString,
                                 bool isReplayMode,
                                 QueryOptions queryOptions,
                                 EventOptions eventOptions,
                                 IProgress<string>? progress,
                                 CancellationToken cancellationToken)
    {
        long rowCount = 0;

        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync(cancellationToken);

        var logPath = await ExecuteScalar<string>(GetFileLocationSql(), connection, cancellationToken);

        var filePath = $"{logPath}\\{sessionName}.xel";

        List<LogRecord> logRecords = [];
        string? startLsn = null;

        if (preCommandSql.Length > 0)
        {
            progress?.Report("Pre-Trace: ");

            foreach (var preCommand in preCommandSql)
            {
                var itemRowCount = await ExecuteSql(preCommand, connection, cancellationToken);

                if (itemRowCount > -1)
                {
                    progress?.Report($"  {itemRowCount} row(s) affected");
                }
            }
        }

        var spid = await ExecuteScalar<short>("SELECT @@SPID", connection, cancellationToken);

        var createSessionSql = GetCreateSessionSql(sessionName, filePath, spid, isReplayMode, eventOptions);

        await ExecuteSql(createSessionSql, connection, cancellationToken);

        if (queryOptions.ClearBufferPool | isReplayMode)
        {
            // Flush dirty pages either for DROPCLEANBUFFERS or to write the transaction log to disk 
            await ExecuteSql("CHECKPOINT", connection, cancellationToken);
        }

        if (queryOptions.ClearBufferPool)
        {
            // Removes all pages from the buffer pool so pages will come from I/O rather than the cache
            await ExecuteSql("DBCC DROPCLEANBUFFERS", connection, cancellationToken);
        }

        if (queryOptions.DisableReadAhead)
        {
            // Disable pre-fetching page scans for the session
            await ExecuteSql("DBCC TRACEON(652)", connection, cancellationToken);
        }

        if (isReplayMode)
        {
            startLsn = await ExecuteScalar<string?>(
                "SELECT MAX([Current LSN]) FROM fn_dblog(NULL, NULL);", connection, cancellationToken);

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

            await Task.Delay(250, cancellationToken);

            Logger.LogDebug("SQL: {Sql}", commandSql);

            var queryStart = Stopwatch.GetTimestamp();

            var command = new SqlCommand(commandSql, connection);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

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

        if (postCommandSql.Length > 0)
        {
            progress?.Report("Post-Trace: ");

            foreach (var postCommand in postCommandSql)
            {
                var itemRowCount = await ExecuteSql(postCommand, connection, cancellationToken);

                if (itemRowCount > -1)
                {
                    progress?.Report($"  {itemRowCount} row(s) affected");
                }
            }
        }

        return (filePath, rowCount, logRecords);
    }

    private static async Task<T?> ExecuteScalar<T>(string sql,
                                                   SqlConnection connection,
                                                   CancellationToken cancellationToken)
    {
        var result = await new SqlCommand(sql, connection).ExecuteScalarAsync(cancellationToken);

        return (T?)result;
    }

    private async Task<int> ExecuteSql(string sql, SqlConnection connection, CancellationToken cancellationToken)
    {
        Logger.LogDebug("SQL: {Sql}", sql);

        return await new SqlCommand(sql, connection).ExecuteNonQueryAsync(cancellationToken);
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

        if (eventOptions.IncludeMemory)
        {
            sessionEvents.AddRange(_memoryEvents);
        }

        if (eventOptions.IncludeCallStack)
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

internal static partial class PayloadParser
{
    public static (string[] PreCommands, string[] Commands, string[] PostCommands) Parse(ExecuteSqlPayload payload)
    {
        if (payload.TrackedSelection == null)
        {
            return ([], SplitCommands(payload.SqlText), []);
        }

        var length = payload.SqlText.Length;
        
        var start = Math.Clamp(payload.TrackedSelection.Start, 0, length);
        
        var end = Math.Clamp(payload.TrackedSelection.End, start, length);

        var preCommands = payload.SqlText[..start];
        var commands = payload.SqlText[start..end];
        var postCommands = payload.SqlText[end..];

        return (SplitCommands(preCommands), SplitCommands(commands), SplitCommands(postCommands));
    }

    private static string[] SplitCommands(string sql)
    {
        var result = GoRegEx().Split(sql)
                              .Where(value => !string.IsNullOrWhiteSpace(value))
                              .ToArray();

        return result;
    }


    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex GoRegEx();
}