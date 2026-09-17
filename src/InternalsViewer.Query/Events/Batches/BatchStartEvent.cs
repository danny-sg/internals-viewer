namespace InternalsViewer.Query.Events.Batches;

public sealed partial record BatchStartEvent : EngineEvent
{
    public string SqlText
    {
        get;
        set;
    } = string.Empty;

    public override bool IsVisible => false;
}