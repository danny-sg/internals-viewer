using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.TransactionLog.LogRecords;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.TransactionLog;

/// <summary>
/// Reads log records from the transaction log from a start LSN using fn_dblog
/// </summary>
/// <remarks>
/// fn_dblog is used to access the current transaction log in a database.
///
/// The actual transaction log files will be locked by the SQL Server process so the function is used as a proxy to reads logs. The raw
/// bytes are read from the [Log Record] field and parsed rather than relying on the function's interpretation.
///
/// [Log Record] is a varbinary(8000) field, so records larger than 8,000 bytes wil be truncated. This is a limitation of fn_dblog.
/// </remarks>
public sealed class LogRecordReader(ILogger<LogRecordReader> logger)
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
