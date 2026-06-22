namespace InternalsViewer.Replay.Events.EventTypes;

public sealed record IoEvent : EngineEvent
{
    public bool IsRead { get; init; }

    public override string Description => $"Page {(IsRead ? "Read" : "Write")}";
}