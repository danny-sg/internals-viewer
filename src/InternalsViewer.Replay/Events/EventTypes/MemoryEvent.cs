namespace InternalsViewer.Replay.Events.EventTypes;

public sealed record MemoryEvent : EngineEvent
{
    public long UsedMemoryKb;

    public long GrantedMemoryKb;
}