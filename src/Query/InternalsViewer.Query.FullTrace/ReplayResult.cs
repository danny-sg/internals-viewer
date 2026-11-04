using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace;

/// <summary>
/// Raw replay results
/// </summary>
/// <param name="Nodes">
/// Function call reference nodes including parent index, absolute address, instance, and call count
/// </param>
/// <param name="Modules">
/// Modules loaded during the trace recording
/// </param>
/// <param name="CallStore">
/// Function calls including sequence, thread, registers, return value, and raw node index
/// </param>
/// <param name="Timeline">
/// Hierarchical/row layout start and end position for each call per thread
/// </param>
/// <param name="AddressMap">
/// Address sightings from the memory map hook: allocations, constructions, member calls and declared pointers
/// </param>
/// <param name="OutputAddresses">
/// Where each output row's columns read from, at the position the row was sent
/// </param>
/// <param name="PlanNodes">
/// Iterators linked to their plan object and plan node id by the plan node hook
/// </param>
internal sealed record ReplayResult(CallNode[] Nodes,
                                    IReadOnlyList<TraceModule> Modules,
                                    TraceCallStore CallStore,
                                    TraceTimeline Timeline,
                                    ChunkedList<AddressEntry> AddressMap,
                                    TraceBufferPool BufferPool,
                                    List<OutputAddressEntry> OutputAddresses,
                                    IReadOnlyList<IteratorPlanNode> PlanNodes)
{
    public MemoryBlock[] Buffers { get; init; } = [];

    public StackSnapshotResult StackSnapshotResult { get; init; } = StackSnapshotResult.Empty;
}
