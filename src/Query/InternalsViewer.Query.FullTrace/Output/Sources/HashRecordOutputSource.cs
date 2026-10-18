using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

namespace InternalsViewer.Query.FullTrace.Output.Sources;

internal sealed record HashRecordOutputSource(ulong Value) : TraceOutputSource(Value)
{
    public int Ordinal { get; init; }

    public int Offset { get; init; }

    public int Length { get; init; }

    internal override OutputSourceType Type => OutputSourceType.HashRecord;

    internal override RowBufferColumn? GetColumn(RowBuffer row)
    {
        if (row is not { Format: RowBufferFormat.HashTable, Record: { } record } || Ordinal < 1)
        {
            return null;
        }

        if (row.RecordLayout is not null)
        {
            return Ordinal <= row.Columns.Count ? row.Columns[Ordinal - 1] : null;
        }

        return Offset >= 0 ? RecordRowParser.GetFixedColumn(record, Ordinal - 1, Offset, Length) : null;
    }
}
