
namespace InternalsViewer.Query.FullTrace.Output;

internal sealed partial record TraceOutputStructure(ulong OutputRow, IReadOnlyList<ulong> Owners, ulong DirectSelectOwner)
{
    public static TraceOutputStructure Resolve(OutputAddresses addresses, IteratorAllocations allocations)
        => new(addresses.OutputRow,
               [.. addresses.Columns.Select(c => allocations.GetOwner(c, addresses.Position))],
               allocations.GetOwner(addresses.DirectSelect, addresses.Position));
}
