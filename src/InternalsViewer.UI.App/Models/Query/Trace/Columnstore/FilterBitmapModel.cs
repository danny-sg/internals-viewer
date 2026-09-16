using System;
using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results.Steps;

namespace InternalsViewer.UI.App.Models.Query.Trace.Columnstore;

/// <summary>
/// A compressed data filter bitmap of qualifying dictionary ids
/// </summary>
public sealed class FilterBitmapModel
{
    public int RowGroupId { get; set; }

    public int ColumnId { get; set; }

    public string ColumnName { get; set; } = string.Empty;

    public string Form { get; set; } = string.Empty;

    public int EntryCount { get; set; }

    public int QualifyingCount { get; set; }

    public IReadOnlyList<FilterBitmapCell> Cells { get; set; } = [];

    public bool IsTruncated { get; set; }

    public string Title => $"{ColumnName} ({ColumnId})";

    public string SubTitle => IsTruncated
        ? $"{Form} - {QualifyingCount:N0}/{EntryCount:N0} (showing {Cells.Count:N0})"
        : $"{Form} - {QualifyingCount:N0}/{EntryCount:N0}";

    public static FilterBitmapModel From(AccessStep.CompressedDataFilterBitmap step, int maxCells)
    {
        var count = Math.Min(step.DictionaryIds.Count, maxCells);

        var cells = new List<FilterBitmapCell>(count);

        for (var i = 0; i < count; i++)
        {
            cells.Add(new FilterBitmapCell
            {
                Index = i,
                DataId = step.DictionaryIds[i],
                Qualifies = step.Qualifies[i],
                Value = i < step.Values.Count ? step.Values[i] : string.Empty
            });
        }

        return new FilterBitmapModel
        {
            RowGroupId = step.RowGroupId,
            ColumnId = step.ColumnId,
            ColumnName = step.ColumnName,
            Form = FormText(step.Category),
            EntryCount = step.EntryCount,
            QualifyingCount = step.QualifyingCount,
            Cells = cells,
            IsTruncated = step.DictionaryIds.Count > count
        };
    }

    private static string FormText(CompressedFilterCategory category) => category switch
    {
        CompressedFilterCategory.RawBitmap => "RAWBITMAP",
        CompressedFilterCategory.Equality => "EQ",
        CompressedFilterCategory.Comparison => "Comparison",
        _ => "Filter"
    };
}

/// <summary>
/// One dictionary id in a compressed data filter bitmap
/// </summary>
public sealed class FilterBitmapCell
{
    public int Index { get; set; }

    public long DataId { get; set; }

    public bool Qualifies { get; set; }

    public string Value { get; set; } = string.Empty;
}
