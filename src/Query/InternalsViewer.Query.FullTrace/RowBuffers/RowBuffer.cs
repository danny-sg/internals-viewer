namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed record RowBuffer(ulong Function,
                               ulong Instance,
                               int Call,
                               ulong Id,
                               ulong Iterator,
                               ulong Address,
                               IReadOnlyList<RowBufferColumn> Columns)
{
    public override string ToString() => string.Join(Environment.NewLine, Columns.Select(c => $"Column {c.Ordinal}  {c}"));
}
