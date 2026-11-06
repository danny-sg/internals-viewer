using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.Events.Query;

internal class BatchStartEventParser
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