using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Output;

internal sealed partial record TraceOutputStructure(ulong OutputRow, IReadOnlyList<ulong> Owners, ulong DirectSelectOwner)
{
    public static TraceOutputStructure Resolve(OutputAddresses addresses, IteratorAllocations allocations, TraceTimeline timeline)
    {
        var position = timeline.PositionOf(addresses.Position);

        return new TraceOutputStructure(addresses.OutputRow,
                                        [.. addresses.Columns.Select(c => allocations.GetOwner(c, position))],
                                        allocations.GetOwner(addresses.DirectSelect, position));
    }
}
