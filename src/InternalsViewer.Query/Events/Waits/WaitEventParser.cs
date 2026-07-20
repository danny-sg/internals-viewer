using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Waits;

internal class WaitEventParser : IEventParser<WaitEvent>
{
    public static WaitEvent? Map(DatabaseSource? databaseSource, EventResult e)
    {
        var waitType = (WaitType)(e.GetInt("wait_type") ?? 0);

        if (WaitEventFilter.CanIgnore(waitType.ToString()))
        {
            return null;
        }

        var isEnd = e.GetInt("opcode") == 1;

        var waitResource = e.GetUlong("wait_resource");

        var duration = e.GetLong("duration") ?? 0;

        var waitEvent = new WaitEvent
        {
            Name = "Wait",
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            WaitType = waitType,
            IsEnd = isEnd,
            WaitResource = waitResource,
            DurationUs = duration,
        };

        return waitEvent;
    }
}