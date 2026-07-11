using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events.EventTypes;

/// <summary>
/// An engine event anchored to a single page
/// </summary>
/// <remarks>
/// Only events that touch one page carry a <see cref="PageAddress"/>; the base <see cref="EngineEvent"/> does not, so
/// events that span many pages (a <see cref="NonCachedReadEventGroup"/>) or none (waits, memory grants) are not forced
/// to pretend they have a single page.
/// </remarks>
public abstract record PageEngineEvent : EngineEvent
{
    public virtual PageAddress? PageAddress { get; set; }
}
