using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Query.TransactionLog.LogRecords;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.TransactionLog;

public class LogRecordReader(ILogger<LogRecordReader> logger)
{
    public ILogger<LogRecordReader> Logger { get; } = logger;

    public async Task<List<LogRecord>> GetLogRecords(SqlConnection connection,
                                                     string? startLsn,
                                                     string sessionName)
    {
        Logger.LogDebug("Getting log records since LSN {LSN}", startLsn);

        var records = new List<LogRecord>();

        var commandSql = @$"-- LOG_READ_{sessionName}
SELECT [Current LSN], [Log Record] FROM fn_dblog(NULL, NULL) WHERE [Current LSN] > @StartLsn
";
        var command = new SqlCommand(commandSql, connection);

        command.Parameters.AddWithValue("@StartLsn", startLsn);

        await using var reader = await command.ExecuteReaderAsync();

        var ordinalLsn = reader.GetOrdinal("Current LSN");
        var ordinalLogRecord = reader.GetOrdinal("Log Record");

        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(ordinalLogRecord))
            {
                continue;
            }

            var lsn = LogSequenceNumberParser.Parse(reader.GetString(ordinalLsn));

            var data = GetBytes(reader, ordinalLogRecord);

            records.Add(LogRecordParser.Parse(lsn, data));
        }

        return records;
    }

    private static byte[] GetBytes(SqlDataReader reader, int ordinal)
    {
        var length = reader.GetBytes(ordinal, 0, null!, 0, 0);
        var buffer = new byte[length];

        reader.GetBytes(ordinal, 0, buffer, 0, (int)length);

        return buffer;
    }
}
