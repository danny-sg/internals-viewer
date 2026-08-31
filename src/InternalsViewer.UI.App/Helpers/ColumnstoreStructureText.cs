using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.UI.App.Helpers;

public static class ColumnstoreStructureText
{
    public static string? Describe(IReadOnlyList<ColumnstorePageRead> reads)
        => reads.Count == 0
            ? null
            : string.Join("; ", reads.Select(Describe).Where(t => t is { Length: > 0 }));

    public static string? Describe(ColumnstorePageRead? read)
    {
        if (read is null)
        {
            return null;
        }

        var parts = new List<string>(3);

        if (read.RowGroupId >= 0)
        {
            parts.Add($"Row Group {read.RowGroupId}");
        }

        parts.Add(read.ColumnName.Length > 0
                  ? $"Column {read.ColumnName}"
                  : $"Column {read.ColumnId}");

        parts.Add(read.ReadType switch
        {
            ColumnstoreReadType.Dictionary => read.DictionaryId >= 0
                                              ? $"Dictionary {read.DictionaryId}"
                                              : "Dictionary",
            ColumnstoreReadType.DeleteBitmap => "Delete Bitmap",
            _ => read.SegmentId >= 0 ? $"Segment {read.SegmentId}" : "Segment"
        });

        return string.Join(", ", parts);
    }
}
