using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Interfaces.Events;

internal interface IEventParser<out TEvent>
    where TEvent : EngineEvent
{
    static abstract TEvent Map(DatabaseSource databaseSource, EventResult e);
}
