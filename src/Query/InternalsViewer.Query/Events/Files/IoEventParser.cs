using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Files;

public class IoEventParser : IEventParser<IoEvent>
{
    public static IoEvent Map(DatabaseSource? databaseSource, EventResult e)
    {
        var fileId = e.GetShort("file_id") ?? 0;
        var pageId = e.GetInt("page_id") ?? 0;

        return new IoEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            PageAddress = new PageAddress(fileId, pageId),
            IsRead = e.Name?.Contains("read") ?? false
        };
    }
}