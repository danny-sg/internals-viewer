namespace InternalsViewer.Replay.Events;

public sealed record IoEvent : EngineEvent
{
    public bool IsRead { get; init; }

    public override string Description => $"{(IsRead ? "Read" : "Write")}";
}