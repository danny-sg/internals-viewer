using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Reads;

/// <summary>
/// A page-read set of events built from the storage events that make up a single read
/// </summary>
/// <remarks>
/// <see cref="EngineEvent.TimeUs"/> and <see cref="EngineEvent.DurationUs"/> are taken from the suspend spine, not a min/max envelope over
/// the children: child timestamps are quantised to the millisecond, so an envelope stretches to a full 1000us of slop, whereas the folded
/// suspend carries the microsecond-accurate SQL-measured read duration.
/// </remarks>
public sealed record ReadEventGroup : PageEngineEvent, IEventGroup
{
    public required IReadOnlyList<EngineEvent> Events { get; init; }

    public ReadType ReadType { get; init; }

    /// <summary>
    /// The distinct pages this read touched, ordered by page id
    /// </summary>
    /// <remarks>
    /// A read is no longer a single page: a Scatter/Gather read pulls a whole range, so the group links to every page rather than one.
    /// <see cref="PageAddress"/> is the first of them, kept for displays that show a representative.
    /// </remarks>
    public IReadOnlyList<PageAddress> Pages { get; init; } = [];

    public override PageAddress? PageAddress => Pages.Count > 0 ? Pages[0] : null;

    public int PageCount => Pages.Count;

    public override string Description
    {
        get
        {
            if (PageCount > 1)
            {
                return $"{TypeLabel}: {PageCount} pages from {PageAddress}";
            }

            var description = $"{TypeLabel}: {PageAddress}";

            if (PageAddress is { } pageAddress && PageNameHelper.TryGetPageName(pageAddress) is { } pageName)
            {
                return $"{description} - {pageName}";
            }

            if (PageAddress == AllocationUnit?.FirstIamPage)
            {
                return $"{description} - First IAM Page";
            }

            if (PageAddress == AllocationUnit?.RootPage)
            {
                return $"{description} - Root Page";
            }

            return description;
        }
    }

    private string TypeLabel => ReadType == ReadType.NonCached ? "Read (Disk)" : "Read (Buffer Pool)";
}
