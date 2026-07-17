namespace InternalsViewer.Query.Events.Batches;

public sealed record BatchEndEvent : EngineEvent
{
    public string SqlText
    {
        get;
        set;
    } = string.Empty;

    public override bool IsVisible => false;
}