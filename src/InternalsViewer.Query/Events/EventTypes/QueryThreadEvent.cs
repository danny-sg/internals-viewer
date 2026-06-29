namespace InternalsViewer.Query.Events.EventTypes;

public sealed record QueryThreadEvent : EngineEvent
{
    public int NodeId { get; set; }
}