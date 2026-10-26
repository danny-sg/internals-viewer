using System.Drawing;
using InternalsViewer.Query.Results.Enums;

namespace InternalsViewer.Query.Results;

public sealed record ResultColumn(int Ordinal, string Name, Type ClrType, bool IsNullable)
{
    public Color? BackgroundColour { get; set; }

    public bool IsSortable { get; set; }

    public ResultSortDirection? SortDirection { get; set; }

    public string? TypeName { get; set; }

    public string? Detail { get; set; }

    public string? Badge { get; set; }

    public Color? BadgeColour { get; set; }

    public bool IsHeaderLink { get; set; }

    public string? Group { get; set; }

    public int? Width { get; set; }

    public ResultAlignment Alignment { get; set; } = ResultAlignment.Left;
}