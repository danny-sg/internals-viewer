using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events;

/// <summary>
/// An engine event linked to a single page/slot
/// </summary>
public abstract record RowIdentifierEngineEvent : PageEngineEvent
{
    public virtual RowIdentifier? RowIdentifier { get; set; }

    public override string ObjectName => AllocationUnit?.DisplayName is { Length: > 0 } displayName
                                         ? displayName
                                         : (PageAddress is { } page ? PageNameHelper.TryGetPageName(page) : null)
                                           ?? string.Empty;
}