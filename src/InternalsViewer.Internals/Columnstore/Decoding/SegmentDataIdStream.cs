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

    private int[] RunStartRows => field ??= BuildRunStartRows();

    public long GetDataId(int rowOrdinal) => GetRowDataId(rowOrdinal).DataId;

    /// <summary>
    /// Read Segment Row Data Id
    /// </summary>
    public SegmentDataIdSource GetRowDataId(int rowOrdinal)
    {
        if ((uint)rowOrdinal >= (uint)RowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowOrdinal));
        }

        if (Blob.VariableLengthData is { } store)
        {
            var (runIndex, valueOrdinal) = FindValue(rowOrdinal);

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
            return BitSpan.FromBytes(Blob.Header.RleArrayOffset + (entryIndex * Blob.Header.RleEntrySize), Blob.Header.RleValueSize);
        }

        var span = Blob.Bitpack.GetSpan(entry.BitpackIndex + (rowOrdinal - (endRow - entry.Count)));

        return span with { BitOffset = (Blob.Header.BitpackArrayOffset * 8) + span.BitOffset };
    }

    public IEnumerable<SegmentDataIdRun> GetBatchDataIds(int fromRow, int count)
    {
        var end = Math.Min(fromRow + count, RowCount);

        if (Blob.VariableLengthData is not null)
        {
            for (var row = fromRow; row < end; row++)
            {
                var source = GetRowDataId(row);

                yield return new SegmentDataIdRun(source.Origin, source.DataId, source.SourceIndex, row, 1);
            }

            yield break;
        }

        var current = fromRow;

        while (current < end)
        {
            var (entryIndex, endRow) = Seek(current);

            var entry = Blob.RleEntries[entryIndex];

            var startRow = endRow - entry.Count;

            var take = Math.Min(endRow, end) - current;

            if (take <= 0)
            {
                yield break;
            }

            yield return entry.IsValue
                ? new SegmentDataIdRun(SegmentValueOrigin.RleRun, entry.Value, -1, current, take)
                : new SegmentDataIdRun(SegmentValueOrigin.BitPack,
                                       0,
                                       entry.BitpackIndex + (current - startRow),
                                       current,
                                       take);

            current += take;
        }
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

    public (int EntryIndex, int ValueOrdinal) FindValue(int rowOrdinal)
    {
        if (Blob.VariableLengthData is not { } store)
        {
            return (-1, -1);
        }

        var starts = RunStartRows;

        if (rowOrdinal < 0 || rowOrdinal >= starts[^1])
        {
            return (-1, -1);
        }

        var index = Array.BinarySearch(starts, 0, starts.Length - 1, rowOrdinal);

        if (index < 0)
        {
            index = ~index - 1;
        }

        var entry = Blob.RleEntries[index];

        if (entry.PageSlot is not { } address)
        {
            return (-1, -1);
        }

        var ordinal = store.GetOrdinal(address.Page, address.Slot);

        return (index, entry.IsValue ? ordinal : ordinal + (rowOrdinal - starts[index]));
    }

    private int[] BuildRunStartRows()
    {
        var entries = Blob.RleEntries;

        var count = 0;

        while (count < entries.Length && entries[count].Count > 0)
        {
            count++;
        }

        var starts = new int[count + 1];

        for (var i = 0; i < count; i++)
        {
            starts[i + 1] = starts[i] + entries[i].Count;
        }

        return starts;
    }

    private (int EntryIndex, int EndRow) Seek(int rowOrdinal)
    {
        var bookmarks = Blob.Bookmarks;

        var entryIndex = 0;

        int endRow;

        if (bookmarks.Length > 0 && Blob.Header.BookmarkDistance > 0)
        {
            var bookmark = bookmarks[Math.Min(rowOrdinal / Blob.Header.BookmarkDistance, bookmarks.Length - 1)];

            entryIndex = bookmark.GetRleEntryIndex(Blob.Header.RleEntrySize);

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
