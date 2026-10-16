using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

public sealed record DeferredPageValue(PageAddress Page, int Offset, LogSequenceNumber Lsn) : DeferredValue
{
    public override string ToString() => $"Page {Page} Offset {Offset}";
}
