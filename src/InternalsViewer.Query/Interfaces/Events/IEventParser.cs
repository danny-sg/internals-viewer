using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.Interfaces.Events;

internal interface IEventParser<out TEvent>
    where TEvent : EngineEvent
{
    static abstract TEvent? Map(DatabaseSource databaseSource, EventResult e);
}
