using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.Query.FullTrace.Output.Sources;

internal sealed record BufferOutputSource(ulong Value, ulong Address) : TraceOutputSource(Value)
{
    internal override OutputSourceType Type => OutputSourceType.Buffer;

    internal override RowBufferColumn? GetColumn(RowBuffer row)
    {
        if (Address < row.Address)
        {
            return null;
        }

        var ordinal = RowBufferStore.GetColumn(row, Address - row.Address);

        return ordinal >= 0 && ordinal < row.Columns.Count ? row.Columns[ordinal] : null;
    }
}
