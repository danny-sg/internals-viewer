using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Files;

internal class FileEventParser : IEventParser<FileEvent>
{
    public static FileEvent Map(DatabaseSource databaseSource, EventResult e)
    {
        var offset = e.GetLong("offset") ?? 0;
        var size = e.GetLong("size") ?? 0;

        var fileId = e.GetShort("file_id") ?? 0;

        var pageId = e.GetInt("page_id") ?? (int)(offset / 8192);

        var mode = (ReadMode)(e.GetByte("mode") ?? 0);

        return new FileEvent
        {
            Name = e.Name,
            Size = size,
            Offset = offset,
            Mode = mode,
            FileId = fileId,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            PageAddress = new PageAddress(fileId, pageId),
            IsRead = e.Name?.Contains("read") ?? false
        };
    }
}