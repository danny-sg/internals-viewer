using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.FullTrace.Replay;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public sealed partial class TraceBufferPool
{
    internal TraceBufferPool(TraceBufferPage[] pages, Dictionary<(PageAddress Page, ushort DatabaseId), PageData>? layouts = null)
    {
        Pages = pages;

        Layouts = layouts ?? [];

        Buffers = [.. pages.Where(p => p.Buffer != 0).OrderBy(p => p.Buffer).ThenBy(p => p.Position)];

        BufferStarts = [.. Buffers.Select(p => p.Buffer)];
    }

    public static TraceBufferPool Empty { get; } = new([]);

    public int Count => Pages.Length;

    public EngineLayout Layout { get; private init; } = EngineLayout.Latest;

    internal TracePosition? FirstPosition => Pages.Length == 0 ? null : Pages.Min(p => p.Position);

    private TraceBufferPage[] Pages { get; }

    private TraceBufferPage[] Buffers { get; }

    private ulong[] BufferStarts { get; }

    private Dictionary<(PageAddress Page, ushort DatabaseId), PageData> Layouts { get; }

    public bool IsBuffer(ulong address) => Array.BinarySearch(BufferStarts, address) >= 0;

    public PageData? GetPageLayout(PageAddress page, ushort databaseId) => Layouts.GetValueOrDefault((page, databaseId));

    public TraceBufferReference? GetReference(ulong address, TracePosition position)
        => GetFrameReference(address, position) ?? GetBufferReference(address, position);

    public List<TraceBufferPage> GetResidencies(ulong buffer)
    {
        var residencies = new List<TraceBufferPage>();

        for (var index = SortedSearch.FirstAtOrAfter(Buffers, buffer, p => p.Buffer);
             index < Buffers.Length && Buffers[index].Buffer == buffer;
             index++)
        {
            residencies.Add(Buffers[index]);
        }

        return residencies;
    }

    public TracePosition GetReferenceEnd(TraceBufferReference reference, ulong address, TracePosition position)
    {
        var start = address - (ulong)reference.Offset;

        var (pages, getKey) = reference.IsBuffer
            ? (Buffers, (Func<TraceBufferPage, (ulong, TracePosition)>)GetBufferKey)
            : (Pages, GetFrameKey);

        var index = SortedSearch.FirstAfter(pages, (start, position), getKey);

        return index < pages.Length && getKey(pages[index]).Item1 == start ? pages[index].Position : TracePosition.Unknown;
    }

    public TraceBufferReference? GetFrameReference(ulong address, TracePosition position)
    {
        var frame = address - address % PageData.Size;

        var index = SortedSearch.FirstAfter(Pages, (frame, position), GetFrameKey) - 1;

        if (index < 0 || Pages[index].Frame != frame)
        {
            return null;
        }

        var page = Pages[index];

        return new TraceBufferReference(page.Page, page.DatabaseId, (int)(address - frame));
    }

    public ulong? GetRecord(ulong buffer, int slot, TracePosition position)
    {
        var index = SortedSearch.FirstAfter(Buffers, (buffer, position), GetBufferKey) - 1;

        if (index < 0 || Buffers[index].Buffer != buffer)
        {
            return null;
        }

        var page = Buffers[index];

        return GetPageLayout(page.Page, page.DatabaseId)?.OffsetTable is { } offsets && slot >= 0 && slot < offsets.Length
            ? page.Frame + offsets[slot]
            : null;
    }

    internal static TraceBufferPool Build(ReplayBufferPool sightings)
    {
        var pages = new List<TraceBufferPage>();

        foreach (var page in sightings.Sightings.OrderBy(p => p.Frame).ThenBy(p => p.Position))
        {
            if (pages.Count > 0 && pages[^1] is var previous && IsSameResidency(previous, page))
            {
                if (previous.Buffer == 0 && page.Buffer != 0)
                {
                    pages[^1] = previous with { Buffer = page.Buffer };
                }

                continue;
            }

            pages.Add(page);
        }

        var layouts = sightings.SlotLayouts.ToDictionary(l => l.Key, l => CreateLayout(l.Key.Page, l.Value.Offsets, l.Value.Type));

        return new TraceBufferPool([.. pages], layouts);
    }

    internal TraceBufferPool WithLayout(EngineLayout layout)
        => ReferenceEquals(layout, Layout) ? this : new TraceBufferPool(Pages, Layouts) { Layout = layout };

    internal IEnumerable<ulong> GetFrames() => Pages.Select(p => p.Frame).Distinct();

    private TraceBufferReference? GetBufferReference(ulong address, TracePosition position)
    {
        var last = SortedSearch.FirstAfter(BufferStarts, address) - 1;

        if (last < 0 || address - BufferStarts[last] >= (ulong)Layout.Buffer.Size)
        {
            return null;
        }

        var buffer = BufferStarts[last];

        var index = SortedSearch.FirstAfter(Buffers, (buffer, position), GetBufferKey) - 1;

        if (index < 0 || Buffers[index].Buffer != buffer)
        {
            return null;
        }

        var page = Buffers[index];

        return new TraceBufferReference(page.Page, page.DatabaseId, (int)(address - buffer), IsBuffer: true);
    }

    private static PageData CreateLayout(PageAddress page, ushort[] offsets, PageType type)
        => new() { PageAddress = page, OffsetTable = offsets, PageHeader = new PageHeader { PageType = type } };

    private static (ulong, TracePosition) GetFrameKey(TraceBufferPage page) => (page.Frame, page.Position);

    private static (ulong, TracePosition) GetBufferKey(TraceBufferPage page) => (page.Buffer, page.Position);

    private static bool IsSameResidency(TraceBufferPage previous, TraceBufferPage page)
        => previous.Frame == page.Frame && previous.Page == page.Page && previous.DatabaseId == page.DatabaseId;
}
