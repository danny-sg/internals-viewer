using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers.Records;

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

    public byte[]? Record { get; init; }

    public DeferredRecordValue? Location { get; init; }

    public RowRecordLayout? RecordLayout { get; init; }

    public override string ToString() => string.Join(Environment.NewLine, Columns.Select(c => $"Column {c.Ordinal}  {c}"));
}
