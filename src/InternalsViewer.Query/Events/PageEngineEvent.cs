using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events;

/// <summary>
/// An engine event linked to a single page
/// </summary>
public abstract record PageEngineEvent : EngineEvent
{
    public virtual PageAddress? PageAddress { get; set; }

    // AllocationUnit can be the Unknown sentinel with an empty display name (e.g. allocation operations whose
    // alloc_unit_id has no metadata match), so an empty name falls through to the special page names
    public override string ObjectName => AllocationUnit?.DisplayName is { Length: > 0 } displayName
                                         ? displayName
                                         : (PageAddress is { } page ? PageNameHelper.TryGetPageName(page) : null)
                                           ?? string.Empty;
}