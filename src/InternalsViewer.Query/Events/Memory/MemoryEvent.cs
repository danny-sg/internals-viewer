namespace InternalsViewer.Query.Events.Memory;

public sealed record MemoryEvent : EngineEvent
{
    public long? UsedMemoryKb;

    public long? GrantedMemoryKb;

    public long? AdditionalMemoryBeforeKb;

    public long? AdditionalMemoryAfterKb;
}