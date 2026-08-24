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
            var (runIndex, valueOrdinal) = LocateValue(rowOrdinal);

            // A wide value is the value rather than an id, so there is no number to report for it
            var dataId = valueOrdinal < 0 || store.IsWide ? 0 : store.GetValue(valueOrdinal);

            return new SegmentDataIdSource(dataId, SegmentValueOrigin.VariableLengthData, runIndex, valueOrdinal);
        }

        var (entryIndex, endRow) = Seek(rowOrdinal);

        var entry = Blob.RleEntries[entryIndex];

        if (entry.IsValue)
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

        if (entry.IsValue)
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

            if (!entry.IsValue)
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

    /// <summary>
    /// Which value of the store a row reads, the RLE array being what maps one to the other
    /// </summary>
    /// <remarks>
    /// A run whose value is negative reads the store in order from wherever the runs before it left off, and any
    /// other run repeats the one value it takes. Whether that value is null is the store's own business, a null
    /// being an entry the page's offset array marks rather than anything the run says.
    /// </remarks>
    public (int EntryIndex, int ValueOrdinal) LocateValue(int rowOrdinal)
    {
        if (Blob.VariableLengthData is not { } store)
        {
            return (-1, -1);
        }

        var start = 0;

        for (var i = 0; i < Blob.RleEntries.Length; i++)
        {
            var entry = Blob.RleEntries[i];

            if (entry.Count == 0)
            {
                break;
            }

            if (rowOrdinal < start + entry.Count)
            {
                if (entry.PageSlot is not { } address)
                {
                    return (-1, -1);
                }

                var ordinal = store.GetOrdinal(address.Page, address.Slot);

                return (i, entry.IsValue ? ordinal : ordinal + (rowOrdinal - start));
            }

            start += entry.Count;
        }

        return (-1, -1);
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
