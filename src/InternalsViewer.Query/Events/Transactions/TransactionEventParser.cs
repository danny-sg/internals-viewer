using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Transactions;

internal class TransactionEventParser : IEventParser<TransactionEvent>
{
    public static TransactionEvent Map(DatabaseSource? databaseSource, EventResult e)
    {
        return new TransactionEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            TransactionId = e.GetLong("transaction_id") ?? 0,
            State = (TransactionState)(e.GetInt("transaction_state") ?? 0),
        };
    }
}