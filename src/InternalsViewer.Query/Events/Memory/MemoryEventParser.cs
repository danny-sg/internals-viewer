using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Memory;

internal class MemoryEventParser : IEventParser<MemoryEvent>
{
    public static MemoryEvent Map(DatabaseSource databaseSource, EventResult e)
    {
        switch (e.Name)
        {
            case "memory_grant_updated_by_feedback":
                return new MemoryEvent
                {
                    Name = e.Name,
                    Timestamp = e.Timestamp,
                    DatabaseId = e.GetDatabaseId(),
                    AdditionalMemoryBeforeKb = e.GetLong("ideal_additional_memory_before_kb") ?? 0,
                    AdditionalMemoryAfterKb = e.GetLong("ideal_additional_memory_after_kb") ?? 0,
                    DurationUs = e.GetLong("duration") ?? 0
                };
            default:
                return new MemoryEvent
                {
                    Name = e.Name,
                    Timestamp = e.Timestamp,
                    DatabaseId = e.GetDatabaseId(),
                    UsedMemoryKb = e.GetLong("used_memory_kb") ?? 0,
                    GrantedMemoryKb = e.GetLong("granted_memory_kb") ?? 0,
                    DurationUs = e.GetLong("duration") ?? 0
                };
        }
    }
}