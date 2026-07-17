using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Batches;

internal class BatchStartEventParser : IEventParser<BatchStartEvent>
{
    public static BatchStartEvent Map(DatabaseSource? databaseSource, EventResult e)
    {
        return new BatchStartEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            SqlText = e.GetString("batch_text")
        };
    }
}