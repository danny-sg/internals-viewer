using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Events.Operators;

public sealed record QueryThreadEvent : EngineEvent
{
    public int NodeId { get; set; }
}