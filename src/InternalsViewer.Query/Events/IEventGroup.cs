using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Events;

/// <summary>
/// A consolidated event that owns the raw events it was built from (a read episode, a lock escalation chain, …)
/// </summary>
/// <remarks>
/// The events grid flattens, and the timeline surfaces, a group's <see cref="Events"/> the same way, so a grouping type
/// implements this rather than each consumer special-casing every group.
/// </remarks>
public interface IEventGroup
{
    IReadOnlyList<EngineEvent> Events { get; }
}
