namespace InternalsViewer.Query.Events.Batches;

public sealed record BatchStartEvent : EngineEvent
{
    public string SqlText
    {
        get;
        set;
    } = string.Empty;
}