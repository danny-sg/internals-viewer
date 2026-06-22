namespace InternalsViewer.Replay.Events.EventTypes;

public sealed record QueryThreadEvent : EngineEvent
{
    public int ThreadId { get; set; }

    public int NodeId { get; set; }
}