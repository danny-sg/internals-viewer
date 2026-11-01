namespace InternalsViewer.Query.FullTrace.Memory.History.Bindings;

public sealed record ScanBindings(ulong Iterator, ulong Bindings, ulong Columns, ulong RowBuffer)
{
    public bool Contains(ulong address) => address != 0 && (address == Bindings || address == Columns || address == RowBuffer);
}
