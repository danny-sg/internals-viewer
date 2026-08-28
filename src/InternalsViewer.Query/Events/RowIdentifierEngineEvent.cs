using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events;

/// <summary>
/// An engine event linked to a single page/slot
/// </summary>
public abstract record RowIdentifierEngineEvent : PageEngineEvent
{
    public virtual RowIdentifier? RowIdentifier { get; set; }
}