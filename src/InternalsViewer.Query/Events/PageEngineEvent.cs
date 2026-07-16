using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events;

/// <summary>
/// An engine event linked to a single page
/// </summary>
public abstract record PageEngineEvent : EngineEvent
{
    public virtual PageAddress? PageAddress { get; set; }

    public override string ObjectName => AllocationUnit?.DisplayName
                                         ?? (PageAddress is { } page ? PageNameHelper.TryGetPageName(page) : null)
                                         ?? string.Empty;
}
