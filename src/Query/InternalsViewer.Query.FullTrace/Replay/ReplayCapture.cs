using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;
using InternalsViewer.Query.FullTrace.Replay.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Replay;

/// <summary>
/// Raw replay results
/// </summary>
/// <param name="CallNodes">
/// Function call reference nodes including parent index, absolute address, instance, and call count
/// </param>
/// <param name="Modules">
/// Modules loaded during the trace recording
/// </param>
/// <param name="CallLog">
/// Function calls including sequence, thread, registers, return value, and raw node index
/// </param>
/// <param name="Spans">
/// Hierarchical/row layout start and end position for each call per thread
/// </param>
/// <param name="AddressEntries">
/// Address sightings from the memory map hook: allocations, constructions, member calls and declared pointers
/// </param>
/// <param name="PlanNodes">
/// Iterators linked to their plan object and plan node id by the plan node hook
/// </param>
internal sealed record ReplayCapture(CallNode[] CallNodes,
                                     IReadOnlyList<TraceModule> Modules,
                                     ReplayCallLog CallLog,
                                     ReplaySpans Spans,
                                     ChunkedList<AddressEntry> AddressEntries,
                                     ReplayBufferPool BufferPool,
                                     IReadOnlyList<IteratorPlanNode> PlanNodes)
{
    public MemoryBlock[] BufferBlocks { get; init; } = [];

    public StackSnapshotResult StackSnapshots { get; init; } = StackSnapshotResult.Empty;
}
