using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Batches;

internal class BatchEndEventParser : IEventParser<BatchEndEvent>
{
    public static BatchEndEvent Map(DatabaseSource? databaseSource, EventResult e)
    {
        return new BatchEndEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            SqlText = e.GetString("batch_text")
        };
    }
}