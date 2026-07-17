using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Latches;

internal class LatchEventParser : IEventParser<LatchEvent>
{
    public static LatchEvent Map(DatabaseSource? databaseSource, EventResult e)
    {
        var address = e.GetUlong("address");

        var latchMode = (LatchMode)(e.GetInt("mode") ?? 0);

        var fileId = e.GetShort("file_id") ?? 0;

        var pageId = e.GetInt("page_id") ?? 0;

        var latchClass = (LatchClass)(e.GetInt("class") ?? 0);

        var latchEvent = new LatchEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            LatchMode = latchMode,
            LatchClass = latchClass,
            LatchAddress = address,
            DurationUs = e.GetLong("duration") ?? 0,
            PageAddress = new PageAddress(fileId, pageId)
        };

        return latchEvent;
    }
}