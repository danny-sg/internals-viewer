using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed record MemoryHistoryCall(CallStackNode? Node,
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
