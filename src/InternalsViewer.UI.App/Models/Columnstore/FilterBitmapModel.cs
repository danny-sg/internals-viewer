using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Columnstore;

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
