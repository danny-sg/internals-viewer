using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.Internals.Columnstore.Decoding;

/// <summary>
/// Projects a compressed row group as rows across the columns that could be read
/// </summary>
public sealed class RowGroupReader(RowGroup rowGroup, IReadOnlyList<SegmentReader> readers, IReadOnlyList<ColumnSegment> skipped)
{
    public RowGroup RowGroup { get; } = rowGroup;

    public IReadOnlyList<SegmentReader> Readers { get; } = readers;

    /// <summary>
    /// Segments whose structure or encoding is not yet understood
    /// </summary>
    public IReadOnlyList<ColumnSegment> Skipped { get; } = skipped;

    public IReadOnlyList<ColumnStoreColumn?> Columns { get; } = [.. readers.Select(r => r.Segment.Column)];

    public int RowCount { get; } = readers.Count > 0 ? readers.Min(r => r.RowCount) : 0;

    public object?[] GetRow(int rowOrdinal)
    {
        var row = new object?[Readers.Count];

        for (var i = 0; i < Readers.Count; i++)
        {
            row[i] = GetValue(i, rowOrdinal);
        }

        return row;
    }

    public object? GetValue(int columnIndex, int rowOrdinal)
    {
        var reader = Readers[columnIndex];

        return reader.GetValue(rowOrdinal);
    }

    public IEnumerable<object?[]> ReadAll()
    {
        for (var i = 0; i < RowCount; i++)
        {
            yield return GetRow(i);
        }
    }

    public int GetColumnIndex(string columnName)
        => Columns.ToList().FindIndex(c => string.Equals(c?.Name, columnName, StringComparison.OrdinalIgnoreCase));
}
