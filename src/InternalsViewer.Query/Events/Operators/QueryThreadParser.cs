using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Operators;

internal class QueryThreadParser : IEventParser<QueryThreadEvent>
{
    public static QueryThreadEvent Map(DatabaseSource databaseSource, EventResult e)
    {
        var threadId = (e.GetInt("thread_id") ?? 0);
        var nodeId = (e.GetInt("node_id") ?? 0);

        return new QueryThreadEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            ThreadId = threadId,
            NodeId = nodeId,
            DurationUs = e.GetLong("total_time_us") ?? 0
        };
    }
}