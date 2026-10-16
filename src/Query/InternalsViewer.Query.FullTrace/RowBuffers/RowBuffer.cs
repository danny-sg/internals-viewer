using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed record RowBuffer(ulong Function,
                               ulong Instance,
                               int Call,
                               TracePosition Position,
                               ulong Iterator,
                               ulong Address,
                               IReadOnlyList<RowBufferColumn> Columns,
                               RowBufferFormat Format = RowBufferFormat.Bindings)
{
    public ulong Batch { get; init; }

    public int BatchSelectionIndex { get; init; } = -1;

    public override string ToString() => string.Join(Environment.NewLine, Columns.Select(c => $"Column {c.Ordinal}  {c}"));
}
