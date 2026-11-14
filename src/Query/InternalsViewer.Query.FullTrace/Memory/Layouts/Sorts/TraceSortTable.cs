using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts;

public sealed record TraceSortTable(ulong Iterator,
                                    ulong Rowset,
                                    ulong Descriptor,
                                    ulong Cursor,
                                    IReadOnlyList<ulong> RecordPages,
                                    IReadOnlyList<ulong> ComparePages,
                                    IReadOnlyList<TraceSortEntry> Entries,
                                    IReadOnlyList<TraceSortRun> Runs)
{
    private const int PageSize = 8192;

    public TraceSortPart? FindPart(ulong address, int length)
    {
        var end = address + (ulong)length;

        foreach (var part in GetParts())
        {
            if (part.Address != 0 && part.Address >= address && part.Address < end)
            {
                return part;
            }
        }

        return null;
    }

    public IEnumerable<TraceSortPart> GetParts()
    {
        yield return new TraceSortPart(TraceSortPartType.Iterator, Iterator);

        yield return new TraceSortPart(TraceSortPartType.Rowset, Rowset);

        yield return new TraceSortPart(TraceSortPartType.Descriptor, Descriptor);

        foreach (var page in RecordPages)
        {
            yield return new TraceSortPart(TraceSortPartType.RecordPage, page);
        }

        foreach (var page in ComparePages)
        {
            yield return new TraceSortPart(TraceSortPartType.ComparePage, page);
        }
    }

    public IEnumerable<TraceSortEntry> GetEntries(ulong page) => Entries.Where(e => e.Address >= page && e.Address < page + PageSize);

    public TraceSortEntry? GetEntry(ulong address) => Entries.FirstOrDefault(e => e.Address == address);
}
