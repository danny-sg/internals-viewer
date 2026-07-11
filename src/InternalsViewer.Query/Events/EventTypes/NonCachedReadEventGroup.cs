using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events.EventTypes;

/// <summary>
/// A page-read episode built from the storage events that make up a single read
/// </summary>
/// <remarks>
/// Owns its constituent events rather than duplicating them; a flatten/SelectMany over <see cref="Events"/> expands
/// the group back into the raw stream.
///
/// <see cref="EngineEvent.TimeUs"/> and <see cref="EngineEvent.DurationUs"/> are taken from the suspend spine, not a
/// min/max envelope over the children: child timestamps are quantised to the millisecond, so an envelope stretches to
/// a full 1000us of slop, whereas the folded suspend carries the microsecond-accurate SQL-measured read duration.
/// </remarks>
public sealed record NonCachedReadEventGroup : EngineEvent
{
    public required IReadOnlyList<EngineEvent> Events { get; init; }

    public ReadKind Kind { get; init; }

    /// <summary>
    /// The distinct pages this read touched, ordered by page id
    /// </summary>
    /// <remarks>
    /// A read is no longer a single page: a Scatter/Gather read pulls a whole range, so the group links to every page
    /// rather than one. <see cref="PageAddress"/> is the first of them, kept for displays that show a representative.
    /// </remarks>
    public IReadOnlyList<PageAddress> Pages { get; init; } = [];

    public PageAddress? PageAddress => Pages.Count > 0 ? Pages[0] : null;

    public int PageCount => Pages.Count;

    public override string Description => PageCount > 1
        ? $"{KindLabel} Read: {PageCount} pages from {PageAddress}"
        : $"{KindLabel} Read: {PageAddress}";

    private string KindLabel => Kind == ReadKind.NonCached ? "Non-Cached" : "Cached";
}
