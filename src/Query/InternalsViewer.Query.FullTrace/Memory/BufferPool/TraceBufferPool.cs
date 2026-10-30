using System.Runtime.CompilerServices;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public sealed partial class TraceBufferPool
{
    internal TraceBufferPool(TraceBufferPage[] pages)
    {
        Pages = pages;

        Buffers = [.. pages.Where(p => p.Buffer != 0).OrderBy(p => p.Buffer).ThenBy(p => p.Position)];

        BufferStarts = [.. Buffers.Select(p => p.Buffer)];
    }

    public static TraceBufferPool Empty { get; } = new([]);

    public int Count => Pages.Length;

    public long Bytes => (Pages.Length + Buffers.Length) * (long)Unsafe.SizeOf<TraceBufferPage>() + BufferStarts.Length * sizeof(ulong);

    private TraceBufferPage[] Pages { get; }

    private TraceBufferPage[] Buffers { get; }

    private ulong[] BufferStarts { get; }

    public bool IsBuffer(ulong address) => Array.BinarySearch(BufferStarts, address) >= 0;

    public TraceBufferReference? GetReference(ulong address, TracePosition position)
        => GetFrameReference(address, position) ?? GetBufferReference(address, position);

    public List<TraceBufferPage> GetResidencies(ulong buffer)
    {
        var residencies = new List<TraceBufferPage>();

        for (var index = SortedSearch.FirstAtOrAfter<TraceBufferPage, ulong>(Buffers, buffer, p => p.Buffer);
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

        var index = SortedSearch.FirstAfter<TraceBufferPage, (ulong, TracePosition)>(pages, (start, position), getKey);

        return index < pages.Length && getKey(pages[index]).Item1 == start ? pages[index].Position : TracePosition.Unknown;
    }

    private TraceBufferReference? GetFrameReference(ulong address, TracePosition position)
    {
        var frame = address - address % PageData.Size;

        var index = SortedSearch.FirstAfter<TraceBufferPage, (ulong, TracePosition)>(Pages, (frame, position), GetFrameKey) - 1;

        if (index < 0 || Pages[index].Frame != frame)
        {
            return null;
        }

        var page = Pages[index];

        return new TraceBufferReference(page.Page, page.DatabaseId, (int)(address - frame));
    }

    private TraceBufferReference? GetBufferReference(ulong address, TracePosition position)
    {
        var last = SortedSearch.FirstAfter(BufferStarts, address) - 1;

        if (last < 0 || address - BufferStarts[last] >= BufferDescriptor.Size)
        {
            return null;
        }

        var buffer = BufferStarts[last];

        var index = SortedSearch.FirstAfter<TraceBufferPage, (ulong, TracePosition)>(Buffers, (buffer, position), GetBufferKey) - 1;

        if (index < 0 || Buffers[index].Buffer != buffer)
        {
            return null;
        }

        var page = Buffers[index];

        return new TraceBufferReference(page.Page, page.DatabaseId, (int)(address - buffer), IsBuffer: true);
    }

    private static (ulong, TracePosition) GetFrameKey(TraceBufferPage page) => (page.Frame, page.Position);

    private static (ulong, TracePosition) GetBufferKey(TraceBufferPage page) => (page.Buffer, page.Position);

    internal sealed class Builder
    {
        private List<TraceBufferPage> Pages { get; } = [];

        public void Add(TraceBufferPage page) => Pages.Add(page);

        public TraceBufferPool Build()
        {
            var pages = new List<TraceBufferPage>();

            foreach (var page in Pages.OrderBy(p => p.Frame).ThenBy(p => p.Position))
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

            return new TraceBufferPool([.. pages]);
        }

        private static bool IsSameResidency(TraceBufferPage previous, TraceBufferPage page)
            => previous.Frame == page.Frame && previous.Page == page.Page && previous.DatabaseId == page.DatabaseId;
    }
}
