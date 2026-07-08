using InternalsViewer.Query.Events.Latches;

namespace InternalsViewer.Query.Events.EventTypes;

public sealed record LatchEvent : EngineEvent
{
    public LatchMode LatchMode { get; init; }

    public LatchClass LatchClass { get; init; }

    public override string Description => $"Latch: {PageAddress} {LatchMode}";
}