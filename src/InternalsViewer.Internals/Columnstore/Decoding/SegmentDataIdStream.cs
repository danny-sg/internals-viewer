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

    public long GetDataId(int rowOrdinal) => GetSource(rowOrdinal).DataId;

    /// <summary>
    /// Reads a row's data id along with the store it came from
    /// </summary>
    public SegmentDataIdSource GetSource(int rowOrdinal)
    {
        if ((uint)rowOrdinal >= (uint)RowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowOrdinal));
        }

        if (Blob.VariableLengthData is { } store)
        {
            // A wide value is the value rather than an id, so there is no number to report for it
            var dataId = store.IsWide ? 0 : store.GetValue(rowOrdinal);

            return new SegmentDataIdSource(dataId, SegmentValueOrigin.VariableLengthData, -1, -1);
        }

        var (entryIndex, endRow) = Seek(rowOrdinal);

        var entry = Blob.RleEntries[entryIndex];

        if (!entry.IsBitpacked)
        {
            return new SegmentDataIdSource(entry.Value, SegmentValueOrigin.RleRun, entryIndex, -1);
        }

        var bitpackIndex = entry.BitpackIndex + (rowOrdinal - (endRow - entry.Count));

        return new SegmentDataIdSource(Blob.Bitpack[bitpackIndex], SegmentValueOrigin.BitPack, entryIndex, bitpackIndex);
    }

    /// <summary>
    /// Bit address of the row within the blob, which is byte aligned for a literal run
    /// </summary>
    public BitSpan GetSpan(int rowOrdinal)
    {
        if (Blob.VariableLengthData is not null)
        {
            return BitSpan.FromBytes(Blob.Header.VariableLengthDataOffset, Blob.Data.Length - Blob.Header.VariableLengthDataOffset);
        }

        var (entryIndex, endRow) = Seek(rowOrdinal);

        var entry = Blob.RleEntries[entryIndex];

        if (!entry.IsBitpacked)
        {
            return BitSpan.FromBytes(Blob.Header.RleArrayOffset + (entryIndex * Blob.Header.RleEntryBytes), Blob.Header.RleEntryBytes / 2);
        }

        var span = Blob.Bitpack.GetSpan(entry.BitpackIndex + (rowOrdinal - (endRow - entry.Count)));

        return span with { BitOffset = (Blob.Header.BitpackArrayOffset * 8) + span.BitOffset };
    }

    public IEnumerable<long> ReadAll()
    {
        if (Blob.VariableLengthData is { } store)
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

        int endRow;

        if (bookmarks.Length > 0 && Blob.Header.BookmarkDistance > 0)
        {
            var bookmark = bookmarks[Math.Min(rowOrdinal / Blob.Header.BookmarkDistance, bookmarks.Length - 1)];

            entryIndex = bookmark.GetRleEntryIndex(Blob.Header.RleEntryBytes);

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
