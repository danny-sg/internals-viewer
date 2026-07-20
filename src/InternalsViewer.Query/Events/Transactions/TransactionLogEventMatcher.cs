using InternalsViewer.Query.TransactionLog;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Events.Transactions;

internal class TransactionLogEventMatcher
{
    private readonly record struct MatchKey(long PartitionId, LogOperation Operation, LogContext Context, long Size);

    private sealed class Bucket
    {
        public Queue<LogRecord> Records { get; } = new();

        public int RecordCount { get; set; }

        public int EventCount { get; set; }
    }

    public static void Match(List<EngineEvent> events, List<LogRecord> logRecords)
    {
        var index = new Dictionary<MatchKey, Bucket>();

        foreach (var record in logRecords)
        {
            MatchKey key;

            if (record is not RowLogRecord rowRecord)
            {
                key = new MatchKey(0, record.Operation, record.Context, record.LogRecordSize);
            }
            else
            {
                key = new MatchKey(rowRecord.PartitionId, record.Operation, record.Context, record.LogRecordSize);
            }

            if (!index.TryGetValue(key, out var bucket))
            {
                bucket = new Bucket();
                index[key] = bucket;
            }

            bucket.Records.Enqueue(record);
            bucket.RecordCount++;
        }

        foreach (var engineEvent in events)
        {
            if (engineEvent is not TransactionLogEvent transactionLogEvent)
            {
                continue;
            }

            var key = new MatchKey(transactionLogEvent.AllocationUnit?.PartitionId ?? 0,
                                   transactionLogEvent.Operation,
                                   transactionLogEvent.Context,
                                   transactionLogEvent.LogRecordSize);

            if (!index.TryGetValue(key, out var bucket) || bucket.Records.Count == 0)
            {
                continue;
            }

            bucket.EventCount++;

            var record = bucket.Records.Dequeue();

            transactionLogEvent.LogRecord = record;
        }
    }
}