using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

namespace InternalsViewer.Query.FullTrace.Output.Sources;

internal sealed record RowsetOutputSource(ulong Value, ulong Rowset) : TraceOutputSource(Value)
{
    public int Ordinal { get; init; } = -1;

    internal ulong Accessor { get; init; }

    internal override OutputSourceType Type => OutputSourceType.Rowset;

    internal override RowBufferColumn? GetColumn(RowBuffer row)
    {
        foreach (var column in row.Columns)
        {
            if (Ordinal >= 0 && column.Binding.Type?.Ordinal == Ordinal)
            {
                return column;
            }
        }

        return row.Location is { } location
            ? ColumnValueParser.Defer(ColumnValueParser.Create(Ordinal, RowBufferBinding.None), location)
            : null;
    }
}
