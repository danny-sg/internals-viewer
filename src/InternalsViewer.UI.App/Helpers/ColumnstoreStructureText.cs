using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Columnstore.Metadata;

namespace InternalsViewer.UI.App.Helpers;

public static class ColumnstoreStructureText
{
    public static string? Describe(IReadOnlyList<ColumnstorePageRead> reads)
    {
        if (reads.Count == 0)
        {
            return null;
        }

        var groups = reads.GroupBy(r => r.RowGroupId)
                          .OrderBy(g => g.Key)
                          .Select(Describe);

        return string.Join("; ", groups);
    }

    private static string Describe(IGrouping<int, ColumnstorePageRead> group)
    {
        var parts = group.Select(Part).Where(p => p.Length > 0).Distinct().Order();

        var detail = string.Join(" / ", parts);

        return group.Key >= 0 ? $"Row Group {group.Key}, {detail}" : detail;
    }

    private static string Part(ColumnstorePageRead read)
    {
        var name = read.ColumnName.Length > 0
                   ? $"{read.ColumnName} ({read.ColumnId})"
                   : $"Column {read.ColumnId}";

        return read.ReadType switch
        {
            ColumnstoreReadType.Dictionary => read.DictionaryId >= 0
                                              ? $"{name} Dictionary {read.DictionaryId}"
                                              : $"{name} Dictionary",
            ColumnstoreReadType.DeleteBitmap => "Delete Bitmap",
            _ => name
        };
    }
}
