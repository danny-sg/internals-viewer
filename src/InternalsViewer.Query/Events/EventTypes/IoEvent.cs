namespace InternalsViewer.Query.Events.EventTypes;

public sealed record IoEvent : EngineEvent
{
    public bool IsRead { get; init; }

    public override string Description => $"Page {(IsRead ? "Read" : "Write")} {PageAddress}";

    public bool IsRoot { get; set; }
}