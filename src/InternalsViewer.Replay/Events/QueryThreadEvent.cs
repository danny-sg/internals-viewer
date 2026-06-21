namespace InternalsViewer.Replay.Events;

public sealed record QueryThreadEvent : EngineEvent
{
    public int ThreadId { get; set; }

    public int NodeId { get; set; }
}