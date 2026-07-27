using System.Drawing;

namespace InternalsViewer.Query.Results;

public sealed record ResultColumn(int Ordinal, string Name, Type ClrType, bool IsNullable)
{
    public Color? BackgroundColour { get; set; }

    public int? Width { get; set; }

    public ResultAlignment Alignment { get; set; } = ResultAlignment.Left;
}

public enum ResultAlignment
{
    Left,
    Center,
    Right
}