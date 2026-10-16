using System.Linq;
using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

internal static class RowBufferDisplay
{
    public static string GetLabel(RowBuffer row, RowBufferColumn column, RowBufferDecoder? decoder)
    {
        if (decoder?.GetColumn(row, column) is not { } structure)
        {
            return $"Column {column.Ordinal}";
        }

        return row.RecordLayout?.IsKey(column.Ordinal) == true ? $"{structure.ColumnName} (Build Key)" : structure.ColumnName;
    }

    public static object GetValue(RowBuffer row, RowBufferColumn column, RowBufferDecoder? decoder, bool decode)
        => decode && decoder?.Decode(row, column) is { } text ? text : column.ToString();

    public static ResultDetails GetDetails(string title, RowBuffer? row, RowBufferDecoder? decoder, bool decode)
    {
        if (row is null)
        {
            return new ResultDetails(title, [], "No Row Buffer");
        }

        var columns = row.Columns.Select(c => new ResultDetail(GetLabel(row, c, decoder), GetValue(row, c, decoder, decode)));

        return new ResultDetails(title, [.. columns]);
    }
}
