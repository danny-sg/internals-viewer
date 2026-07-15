namespace InternalsViewer.Query.Events.Waits;

public sealed record WaitEvent : EngineEvent
{
    public WaitType WaitType { get; set; }

    public ulong? WaitResource { get; set; }

    public override string Description => $"Wait: {WaitType}";

    public bool IsEnd { get; set; }
}