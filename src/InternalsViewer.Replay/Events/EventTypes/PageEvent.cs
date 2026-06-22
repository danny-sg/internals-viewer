namespace InternalsViewer.Replay.Events.EventTypes;

public sealed record PageEvent : EngineEvent
{
    public required string Type { get; init; }

    public override string Description => Type;
}