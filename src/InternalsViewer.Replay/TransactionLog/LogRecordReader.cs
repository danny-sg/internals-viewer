using InternalsViewer.Internals.Engine.Parsers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Replay.TransactionLog;

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
}
