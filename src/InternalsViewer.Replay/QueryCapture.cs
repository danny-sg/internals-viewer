using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Replay.Events;
using InternalsViewer.Replay.Events.EventTypes;
using InternalsViewer.Replay.Plans;
using InternalsViewer.Replay.TransactionLog;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text;
using System.Xml;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Replay;

public sealed class QueryCapture(ILogger<QueryCapture> logger)
{
    public ILogger<QueryCapture> Logger { get; } = logger;

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
        "sqlserver.query_post_execution_showplan",
        "sqlserver.query_memory_grant_usage"
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
        "sqlserver.plan_handle"
    ];

    public async Task<QueryResult> TraceQuery(string sqlText,
                                              string connectionString,
                                              bool clearBufferPool,
                                              bool disableReadAhead,
                                              bool isReplayMode)
    {
        long rowCount = 0;
        var sessionId = $"QueryReplay_{Guid.NewGuid():N}";

        List<EngineEvent>? events;
        List<ExecutionPlan>? executionPlans;

        try
        {
            (var filePath, rowCount, var logRecords) = await RunQueryWithEventSession(sessionId,
                                                                                      sqlText,
                                                                                      connectionString,
                                                                                      clearBufferPool,
                                                                                      disableReadAhead,
                                                                                      isReplayMode);

            (events, executionPlans) = await GetResults(filePath, connectionString, null);
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
                                              bool isReplayMode)
    {
        var connectionString = database.Connection.GetConnectionString();

        var sessionId = $"QueryReplay_{Guid.NewGuid():N}";

        long rowCount = 0;

        List<EngineEvent>? events;
        List<ExecutionPlan>? executionPlans;

        try
        {
            (var filePath, rowCount, var logRecords) = await RunQueryWithEventSession(sessionId,
                                                                                      sqlText,
                                                                                      connectionString,
                                                                                      clearBufferPool,
                                                                                      disableReadAhead,
                                                                                      isReplayMode);

            (events, executionPlans) = await GetResults(filePath, connectionString, database);

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

    private async Task<(List<EngineEvent>, List<ExecutionPlan>)> GetResults(string filePath,
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

                        engineEvent.SequenceId = sequenceId++;
                        engineEvent.TimeMs = (long)(engineEvent.Timestamp - startTimeStamp.Value).TotalMilliseconds;

                        events.Add(engineEvent);
                    }
                }
            }
        }

        connection.Close();

        var orderedEvents = events.OrderBy(e => e.SequenceId).ToList();

        PlanNodeMatcher.Match(orderedEvents, executionPlans);

        ExecutionPlanParser.MergePlanEvents(orderedEvents, executionPlans);

        return (orderedEvents, executionPlans);
    }

    private async Task<(string, long, List<LogRecord> logRecords)>
        RunQueryWithEventSession(string sessionName,
                                 string sqlText,
                                 string connectionString,
                                 bool clearBufferPool,
                                 bool disableReadAhead,
                                 bool isReplayMode)
    {
        long rowCount = 0;

        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync();

        var logPath = (string?)await new SqlCommand(GetFileLocationSql(), connection).ExecuteScalarAsync();

        var filePath = $"{logPath}\\{sessionName}.xel";

        List<LogRecord> logRecords = [];
        string? startLsn = null;

        var spid = await ExecuteScalar<short>("SELECT @@SPID", connection);

        var createSessionSql = GetCreateSessionSql(sessionName, filePath, spid, isReplayMode);

        await ExecuteSql(createSessionSql, connection);

        if (clearBufferPool | isReplayMode)
        {
            // Flush dirty pages either for DROPCLEANBUFFERS or to write the transaction log to disk 
            await ExecuteSql("CHECKPOINT", connection);
        }

        if (clearBufferPool)
        {
            // Removes all pages from the buffer pool so pages will come from I/O rather than the cache
            await ExecuteSql("DBCC DROPCLEANBUFFERS", connection);
        }

        if (disableReadAhead)
        {
            // Disable pre-fetching page scans for the session
            await ExecuteSql("DBCC TRACEON(652)", connection);
        }

        if (isReplayMode)
        {
            startLsn = await ExecuteScalar<string?>(
                "SELECT MAX([Current LSN]) FROM fn_dblog(NULL, NULL);", connection);
        }

        // Session block that should stop the session if there is any failure
        try
        {
            await ExecuteSql(GetStartSessionSql(sessionName), connection);

            if (isReplayMode)
            {
                await ExecuteSql("BEGIN TRANSACTION;", connection);
            }

            await Task.Delay(100);

            Logger.LogDebug("SQL: {Sql}", sqlText);

            await using var reader = await new SqlCommand(sqlText, connection).ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rowCount++;
            }

            await reader.CloseAsync();

            if (isReplayMode)
            {
                logRecords = await GetLogRecords(connection, startLsn, sessionName);

                await ExecuteSql("ROLLBACK;", connection);
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

        return (filePath, rowCount, logRecords);
    }

    private async Task<List<LogRecord>> GetLogRecords(SqlConnection connection, string? startLsn, string sessionName)
    {
        Logger.LogDebug("Getting log records since LSN {LSN}", startLsn);

        var records = new List<LogRecord>();

        var commandSql = @$"-- LOG_READ_{sessionName}
SELECT * FROM fn_dblog(NULL, NULL) WHERE [Current LSN] > @StartLsn
";
        var command = new SqlCommand(commandSql, connection);

        command.Parameters.AddWithValue("@StartLsn", startLsn);

        await using var reader = await command.ExecuteReaderAsync();

        var ordinalLsn = reader.GetOrdinal("Current LSN");
        var ordinalPrevLsn = reader.GetOrdinal("Previous LSN");
        var ordinalTranId = reader.GetOrdinal("Transaction ID");
        var ordinalOperation = reader.GetOrdinal("Operation");
        var ordinalContext = reader.GetOrdinal("Context");
        var ordinalAllocUnitId = reader.GetOrdinal("AllocUnitId");
        var ordinalPartitionId = reader.GetOrdinal("PartitionId");
        var ordinalPageAddress = reader.GetOrdinal("Page ID");
        var ordinalSlotId = reader.GetOrdinal("Slot ID");
        var ordinalRow0 = reader.GetOrdinal("RowLog Contents 0");
        var ordinalRow1 = reader.GetOrdinal("RowLog Contents 1");
        var ordinalRow2 = reader.GetOrdinal("RowLog Contents 2");
        var ordinalBeginTime = reader.GetOrdinal("Begin Time");

        while (await reader.ReadAsync())
        {
            var pageAddressValue = reader.IsDBNull(ordinalPageAddress)
                                   ? null
                                   : reader.GetString(ordinalPageAddress);

            PageAddressParser.TryParse(pageAddressValue ?? string.Empty, out var pageAddress);

            var logRecord = new LogRecord
            {
                Lsn = LogSequenceNumberParser.Parse(reader.GetString(ordinalLsn)),
                PreviousLsn = reader.IsDBNull(ordinalPrevLsn)
                    ? default
                    : LogSequenceNumberParser.Parse(reader.GetString(ordinalPrevLsn)),

                LogTransactionId = reader.IsDBNull(ordinalTranId)
                    ? string.Empty
                    : reader.GetString(ordinalTranId),

                Operation = reader.IsDBNull(ordinalOperation)
                    ? string.Empty
                    : reader.GetString(ordinalOperation),

                Context = reader.IsDBNull(ordinalContext)
                    ? string.Empty
                    : reader.GetString(ordinalContext),

                AllocationUnitId = TryGetLong(reader, ordinalAllocUnitId),
                PartitionId = TryGetLong(reader, ordinalPartitionId),
                SlotId = TryGetInt(reader, ordinalSlotId),
                PageAddress = pageAddress,

                RowLogContents0 = TryGetBytes(reader, ordinalRow0),
                RowLogContents1 = TryGetBytes(reader, ordinalRow1),
                RowLogContents2 = TryGetBytes(reader, ordinalRow2),

                TransactionId = null,
                SequenceId = null
            };

            var beginTimeValue = reader.IsDBNull(ordinalBeginTime) 
                                 ? null 
                                 : reader.GetString(ordinalBeginTime);

            if (DateTime.TryParse(beginTimeValue, out var beginTime))
            {
                logRecord.ApproximateTime = beginTime;
            }

            records.Add(logRecord);
        }

        return records;
    }

    private static int? SafeGetOrdinal(SqlDataReader reader, string name)
    {
        try
        {
            return reader.GetOrdinal(name);
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static int? TryGetInt(SqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? (int?)null : reader.GetInt32(ordinal);
    }

    private static long? TryGetLong(SqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? (int?)null : reader.GetInt64(ordinal);
    }

    private static short? TryGetShort(SqlDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? (short?)null : reader.GetInt16(ordinal);
    }


    private static DateTime? TryGetDateTime(SqlDataReader reader, int? ordinal)
    {
        if (ordinal == null || reader.IsDBNull(ordinal.Value))
        {
            return null;
        }

        return reader.GetDateTime(ordinal.Value);
    }


    static byte[] TryGetBytes(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return Array.Empty<byte>();
        }

        var length = reader.GetBytes(ordinal, 0, null!, 0, 0);
        var buffer = new byte[length];

        reader.GetBytes(ordinal, 0, buffer, 0, (int)length);

        return buffer;
    }




    private async Task<T?> ExecuteScalar<T>(string sql, SqlConnection connection)
    {
        var result = await new SqlCommand(sql, connection).ExecuteScalarAsync();

        return (T?)result;
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

    private string GetCreateSessionSql(string sessionName, string filePath, short spid, bool isReplayMode)
    {
        var sessionEvents = isReplayMode ? [.. _events, .. _logEvents] : _events;

        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"CREATE EVENT SESSION [{sessionName}] ON SERVER");

        for (var i = 0; i < sessionEvents.Length; i++)
        {
            var eventName = sessionEvents[i];

            stringBuilder.Append($"ADD EVENT {eventName}");

            if (_actions.Length > 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append("(\n    ACTION (");

                stringBuilder.Append(string.Join(", ", _actions));

                stringBuilder.Append(")\n");
                stringBuilder.Append("    WHERE (");

                stringBuilder.Append($"sqlserver.session_id = {spid}");
                stringBuilder.Append($" AND sqlserver.sql_text NOT LIKE '%LOG_READ_{sessionName}%'");

                stringBuilder.Append(")");

                stringBuilder.Append("\n)");
            }

            if (i < sessionEvents.Length - 1)
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