using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

public sealed record DeferredRecordValue(PageAddress Page, int Slot) : DeferredValue
{
    public override string ToString() => $"Page {Page} Slot {Slot}";
}
