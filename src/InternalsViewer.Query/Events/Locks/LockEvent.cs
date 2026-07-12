using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Events.Locks;

public sealed record LockEvent : PageEngineEvent
{
    public LockMode LockMode { get; init; }

    public LockResourceType ResourceType { get; init; }

    public RowIdentifier? RowIdentifier { get; set; }

    public string? KeyHash { get; set; }

    public override string Description => $"Lock: {LockMode}/{ResourceType}";
}