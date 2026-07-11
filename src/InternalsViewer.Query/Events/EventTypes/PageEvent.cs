namespace InternalsViewer.Query.Events.EventTypes;

public sealed record PageEvent : PageEngineEvent
{
    public required string Type { get; init; }

    public override string Description => Type;
}