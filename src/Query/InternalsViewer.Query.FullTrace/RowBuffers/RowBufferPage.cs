using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed record RowBufferPage(PageAddress Page, int Offset, LogSequenceNumber Lsn)
{
    public override string ToString() => $"Page {Page} Offset {Offset}";
}
