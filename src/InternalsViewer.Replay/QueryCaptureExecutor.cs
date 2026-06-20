using Microsoft.Data.SqlClient;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Replay.Events;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Replay;

public sealed record RawResult
{
    public required string QueryPlan { get; set; }

    public required string Events { get; set; }
}

public sealed class KeyHashLookup(ILogger<KeyHashLookup> logger)
{
    private const int BatchSize = 2000;

    public ILogger<KeyHashLookup> Logger { get; } = logger;

    public static async Task<Dictionary<string, RowIdentifier>> GetKeyHashRowIdentifiers(string objectName,
                                                                                         List<string> hashes,
                                                                                         string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var result = new Dictionary<string, RowIdentifier>();

        for (var offset = 0; offset < hashes.Count; offset += BatchSize)
        {
            var batch = hashes.Skip(offset).Take(BatchSize).ToList();

            var paramNames = batch.Select((_, i) => $"@h{i}").ToList();

            var sql = $@"
SELECT %%physloc%% AS RowIdentifier
      ,%%lockres%% AS [LockHash]
FROM   {objectName}
WHERE  %%lockres%% IN ({string.Join(", ", paramNames)})";

            await using var command = new SqlCommand(sql, connection);

            for (var i = 0; i < batch.Count; i++)
            {
                command.Parameters.AddWithValue($"@h{i}", batch[i]);
            }

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var rowIdentifier = reader.GetSqlBinary(0);
                var lockHash = reader.GetString(1);

                result[lockHash] = new RowIdentifier(rowIdentifier.Value);
            }
        }

        return result;
    }
}

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
        "sqlserver.physical_page_write"
    ];

    private readonly string[] _actions =
    [
        "sqlserver.session_id",
        "sqlserver.request_id",
        "sqlserver.sql_text",
        "sqlserver.database_id",
        "sqlserver.plan_handle"
    ];

    public async Task<List<EngineEvent>> TraceQuery(string sqlText,
                                                    string connectionString,
                                                    bool clearBufferPool)
    {
        var sessionName = $"QueryReplay_{Guid.NewGuid():N}";

        var filePath = await RunQueryWithEventSession(sessionName, sqlText, connectionString, clearBufferPool);

        var events = await ParseEventResults(filePath, connectionString);

        return events;
    }

    public async Task<List<EngineEvent>> TraceQuery(string sqlText,
                                                    DatabaseSource database,
                                                    bool clearBufferPool)
    {
        var connectionString = database.Connection.GetConnectionString();

        var sessionName = $"QueryReplay_{Guid.NewGuid():N}";

        var filePath = await RunQueryWithEventSession(sessionName, sqlText, connectionString, clearBufferPool);

        var events = await ParseEventResults(filePath, database);

        await GetEventKeyAddresses(events, database.AllocationUnits, connectionString);

        return events;
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

    private async Task<List<EngineEvent>> ParseEventResults(string filePath, string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);

        var events = new List<EngineEvent>();

        var resultsSql = GetResultsSql(filePath);

        await connection.OpenAsync();

        var sequenceId = 0;

        await using (var reader = await new SqlCommand(resultsSql, connection).ExecuteReaderAsync())
        {
            Logger.LogDebug("SQL: {Sql}", resultsSql);

            while (await reader.ReadAsync())
            {
                var xml = reader.GetString(0);

                var engineEvent = EventParser.ParseEvent(xml);

                if (engineEvent is not null)
                {
                    engineEvent.SequenceId = sequenceId++;
                    events.Add(engineEvent);
                }
            }
        }

        connection.Close();

        EventParser.SetRelativeTimestamps(events);

        return events.OrderBy(e => e.SequenceId).ToList();
    }

    private async Task<List<EngineEvent>> ParseEventResults(string filePath, DatabaseSource database)
    {
        var connectionString = database.Connection.GetConnectionString();

        await using var connection = new SqlConnection(connectionString);

        var events = new List<EngineEvent>();

        var resultsSql = GetResultsSql(filePath);

        await connection.OpenAsync();

        await using (var reader = await new SqlCommand(resultsSql, connection).ExecuteReaderAsync())
        {
            Logger.LogDebug("SQL: {Sql}", resultsSql);

            var sequenceId = 0;

            while (await reader.ReadAsync())
            {
                var xml = reader.GetString(0);

                var engineEvent = EventParser.ParseEvent(xml, database);

                if (engineEvent is not null)
                {
                    engineEvent.SequenceId = sequenceId++;
                    events.Add(engineEvent);
                }
            }
        }

        connection.Close();

        EventParser.SetRelativeTimestamps(events);

        return events.OrderBy(e => e.SequenceId).ToList();
    }

    private async Task<string> RunQueryWithEventSession(string sessionName,
                                                        string sqlText,
                                                        string connectionString,
                                                        bool clearBufferPool)
    {
        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync();

        var logPath = (string?)await new SqlCommand(GetFileLocationSql(), connection).ExecuteScalarAsync();

        var filePath = $"{logPath}\\{sessionName}.xel";

        var spid = (short)(await new SqlCommand("SELECT @@SPID", connection).ExecuteScalarAsync() ?? 0);

        var createSessionSql = GetCreateSessionSql(sessionName, filePath, spid);

        var startSessionSql = GetStartSessionSql(sessionName);

        var stopSessionSql = GetStopSessionSql(sessionName);

        await ExecuteSql(createSessionSql, connection);

        if (clearBufferPool)
        {
            await ExecuteSql("CHECKPOINT", connection);
            await ExecuteSql("DBCC DROPCLEANBUFFERS", connection);
        }

        await ExecuteSql("SET STATISTICS XML ON;", connection);

        await ExecuteSql(startSessionSql, connection);

        await Task.Delay(100);

        Logger.LogDebug("SQL: {Sql}", sqlText);

        await using (var reader = await new SqlCommand(sqlText, connection).ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
            }
        }

        await Task.Delay(100);

        await ExecuteSql(stopSessionSql, connection);

        connection.Close();

        return filePath;
    }

    private async Task ExecuteSql(string sql, SqlConnection connection)
    {
        Logger.LogDebug("SQL: {Sql}", sql);

        await new SqlCommand(sql, connection).ExecuteNonQueryAsync();
    }

    private string GetResultsSql(string filename)
    {
        return $@"
    SELECT event_data
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

    private string GetCreateSessionSql(string sessionName, string filePath, int spid)
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
