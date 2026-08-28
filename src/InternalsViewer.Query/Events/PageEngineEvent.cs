using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events;

/// <summary>
/// An engine event linked to a single page
/// </summary>
public abstract record PageEngineEvent : EngineEvent
{
    public virtual PageAddress? PageAddress { get; set; }

    public override string ObjectName => (PageAddress is { } page ? PageNameHelper.TryGetPageName(page) : null)
                                         ?? (AllocationUnit?.DisplayName is { Length: > 0 } displayName
                                             ? displayName
                                             : string.Empty);
}