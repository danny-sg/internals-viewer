using System.Globalization;
using System.Text;

namespace InternalsViewer.Mcp.Formatting;

public sealed class TextTable(params string[] headers)
{
    private string[] Headers { get; } = headers;

    private List<string[]> Rows { get; } = [];

    public int Count => Rows.Count;

    public TextTable Add(params object?[] cells)
    {
        Rows.Add([.. cells.Select(Cell)]);

        return this;
    }

    public void Write(StringBuilder builder)
    {
        var widths = Headers.Select(h => h.Length).ToArray();

        foreach (var row in Rows)
        {
            for (var index = 0; index < widths.Length && index < row.Length; index++)
            {
                widths[index] = Math.Max(widths[index], row[index].Length);
            }
        }

        builder.AppendLine(Line(Headers, widths));

        builder.AppendLine(string.Join("  ", widths.Select(w => new string('-', w))));

        foreach (var row in Rows)
        {
            builder.AppendLine(Line(row, widths));
        }
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        Write(builder);

        return builder.ToString();
    }

    private static string Line(string[] cells, int[] widths)
        => string.Join("  ", cells.Select((c, i) => i < widths.Length ? c.PadRight(widths[i]) : c)).TrimEnd();

    private static string Cell(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        bool flag => flag ? "True" : "False",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
