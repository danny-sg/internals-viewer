using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.Events.Query;

internal class BatchEndEventParser
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