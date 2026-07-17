using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Interfaces.Events;
using InternalsViewer.Query.TransactionLog;

namespace InternalsViewer.Query.Events.Transactions;

/// <summary>
/// Transaction log operation event parser
/// </summary>
internal class TransactionLogEventParser : IEventParser<TransactionLogEvent>
{
    public static TransactionLogEvent Map(DatabaseSource databaseSource, EventResult e)
    {
        return new TransactionLogEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            Operation = (LogOperation)(e.GetInt("operation") ?? 0),
            Context = (LogContext)(e.GetInt("context") ?? 0),
            AllocationUnitId = e.GetLong("alloc_unit_id") ?? 0,
            TransactionId = e.GetInt("transaction_id")
        };
    }
}