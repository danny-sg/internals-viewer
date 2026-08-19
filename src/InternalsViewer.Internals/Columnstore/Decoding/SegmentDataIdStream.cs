using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Columnstore.Decoding;

/// <summary>
/// Reads the data id of any row in a segment, resolving RLE runs and bit packed runs
/// </summary>
public sealed class SegmentDataIdStream(SegmentBlob blob)
{
    public int RowCount { get; } = blob.RowCount;

    private SegmentBlob Blob { get; } = blob;

    public long GetDataId(int rowOrdinal)
    {
        if ((uint)rowOrdinal >= (uint)RowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowOrdinal));
        }

        if (Blob.ValueStore is { } store)
        {
            return store.GetValue(rowOrdinal);
        }

        var (entryIndex, endRow) = Seek(rowOrdinal);

        var entry = Blob.RleEntries[entryIndex];

        if (!entry.IsBitpacked)
        {
            return entry.Value;
        }

        return Blob.Bitpack[entry.BitpackIndex + (rowOrdinal - (endRow - entry.Count))];
    }

    /// <summary>
    /// Bit address of the row within the blob, which is byte aligned for a literal run
    /// </summary>
    public BitSpan GetSpan(int rowOrdinal)
    {
        if (Blob.ValueStore is not null)
        {
            return BitSpan.FromBytes(Blob.ValueStoreOffset, Blob.Data.Length - Blob.ValueStoreOffset);
        }

        var (entryIndex, endRow) = Seek(rowOrdinal);

        var entry = Blob.RleEntries[entryIndex];

        if (!entry.IsBitpacked)
        {
            return BitSpan.FromBytes(Blob.RleArrayOffset + (entryIndex * Blob.RleEntryBytes), Blob.RleEntryBytes / 2);
        }

        var span = Blob.Bitpack.GetSpan(entry.BitpackIndex + (rowOrdinal - (endRow - entry.Count)));

        return span with { BitOffset = (Blob.BitpackArrayOffset * 8) + span.BitOffset };
    }

    public IEnumerable<long> ReadAll()
    {
        if (Blob.ValueStore is { } store)
        {
            for (var i = 0; i < store.ValueCount; i++)
            {
                yield return store.GetValue(i);
            }

            yield break;
        }

        foreach (var entry in Blob.RleEntries)
        {
            if (entry.Count == 0)
            {
                continue;
            }

            if (entry.IsBitpacked)
            {
                for (var i = 0; i < entry.Count; i++)
                {
                    yield return Blob.Bitpack[entry.BitpackIndex + i];
                }
            }
            else
            {
                for (var i = 0; i < entry.Count; i++)
                {
                    yield return entry.Value;
                }
            }
        }
    }

    private (int EntryIndex, int EndRow) Seek(int rowOrdinal)
    {
        var bookmarks = Blob.Bookmarks;

        var entryIndex = 0;

        var endRow = 0;

        if (bookmarks.Length > 0 && Blob.BookmarkDistance > 0)
        {
            var bookmark = bookmarks[Math.Min(rowOrdinal / Blob.BookmarkDistance, bookmarks.Length - 1)];

            entryIndex = bookmark.GetRleEntryIndex(Blob.RleEntryBytes);
            endRow = bookmark.EndRow;
        }
        else
        {
            endRow = Blob.RleEntries[0].Count;
        }

        while (rowOrdinal >= endRow && entryIndex + 1 < Blob.RleEntries.Length)
        {
            entryIndex++;
            endRow += Blob.RleEntries[entryIndex].Count;
        }

        return (entryIndex, endRow);
    }
}
