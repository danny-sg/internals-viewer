namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;

public sealed record ScanBindingReference(ulong Iterator, ulong Bindings, ulong Columns, ulong RowBuffer)
{
    public bool Contains(ulong address) => address != 0 && (address == Bindings || address == Columns || address == RowBuffer);
}
