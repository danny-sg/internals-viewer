namespace InternalsViewer.Replay.Events.EventTypes;

public sealed record BatchStartEvent : EngineEvent
{
    public string SqlText
    {
        get;
        set;
    } = string.Empty;
}