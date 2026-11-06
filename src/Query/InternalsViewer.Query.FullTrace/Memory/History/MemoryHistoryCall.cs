using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed record MemoryHistoryCall(CallTreeNode? Node,
                                       ulong Iterator,
                                       int CallIndex,
                                       TracePosition Position,
                                       int LastWrite,
                                       int ChangeCount,
                                       IReadOnlyList<MemoryHistoryRegion> Regions,
                                       IReadOnlyList<MemoryHistoryRegion> NetRegions)
{
    public bool IsAllocation { get; init; }

    public int NetByteCount => NetRegions.Sum(r => r.Length);
}
