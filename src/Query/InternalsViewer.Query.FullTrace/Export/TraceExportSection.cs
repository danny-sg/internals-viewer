using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using System.IO.Compression;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.FullTrace.Export;

internal sealed class TraceExportSection : IQueryExportSection
{
    internal const string CallTreeEntry = "fulltrace/calltree.jsonl";

    internal const string FramesEntry = "fulltrace/frames.jsonl";

    internal const string CallStoreEntry = "fulltrace/calllog.bin";

    internal const string TimelineEntry = "fulltrace/timeline.bin";

    internal const string AddressMapEntry = "fulltrace/addressmap.bin";

    internal const string MemoryObjectsEntry = "fulltrace/memoryobjects.bin";

    internal const string ObjectPoolSnapshotEntry = "fulltrace/objectpool.bin";

    internal const string PlanNodesEntry = "fulltrace/plannodes.bin";

    internal const string MemoryHistoryEntry = "fulltrace/memoryhistory.bin";

    internal const string BufferPoolEntry = "fulltrace/bufferpool.bin";

    internal const string PageReadsEntry = "fulltrace/pagereads.bin";

    public bool CanExport(QueryExportContent content) => content.ExtraOf<TraceResult>() is not null;

    public void Write(QueryExportWriter writer, QueryExportContent content)
    {
        var fullTrace = content.ExtraOf<TraceResult>()!;

        var nodeIds = TraceCallTreeExport.Write(writer, fullTrace.CallTree, fullTrace.Timeline.MappedNodes);

        writer.WriteBinary(CallStoreEntry, CompressionLevel.NoCompression, fullTrace.CallStore.Write);

        writer.WriteBinary(TimelineEntry,
                           CompressionLevel.Fastest,
                           w =>
                           {
                               fullTrace.Timeline.Write(w, n => nodeIds[n]);

                               fullTrace.Allocation.Write(w);
                           });

        writer.WriteBinary(AddressMapEntry, CompressionLevel.Fastest, fullTrace.AddressMap.Write);

        writer.WriteBinary(MemoryObjectsEntry, CompressionLevel.Fastest, fullTrace.MemoryObjects.Write);

        writer.WriteBinary(ObjectPoolSnapshotEntry, CompressionLevel.Fastest, fullTrace.ObjectPool.Write);

        writer.WriteBinary(PlanNodesEntry, CompressionLevel.Fastest, w => IteratorPlanNode.Write(w, fullTrace.PlanNodes));

        writer.WriteBinary(MemoryHistoryEntry, CompressionLevel.Fastest, fullTrace.MemoryHistory.Write);

        writer.WriteBinary(BufferPoolEntry, CompressionLevel.Fastest, fullTrace.BufferPool.Write);

        writer.WriteBinary(PageReadsEntry, CompressionLevel.Fastest, fullTrace.PageReads.Write);
    }

    public IRunExtra? Read(QueryExportReader reader)
    {
        if (TraceCallTreeExport.Read(reader) is not (var tree, var nodes)
            || !reader.ReadBinary(CallStoreEntry, TraceCallStore.IsSupported)
            || !reader.ReadBinary(TimelineEntry, TraceTimeline.IsSupported))
        {
            return null;
        }

        var callStore = reader.ReadBinary(CallStoreEntry, TraceCallStore.Read);

        var (timeline, memory) = reader.ReadBinary(TimelineEntry, r => (TraceTimeline.Read(r, nodes), AllocationIndex.Read(r)));

        callStore.MapNodes(timeline.MappedNodes);

        IReadOnlyList<IteratorPlanNode> planNodes = reader.HasEntry(PlanNodesEntry)
            ? reader.ReadBinary(PlanNodesEntry, IteratorPlanNode.Read)
            : [];

        var builder = new TraceResultBuilder(tree, callStore, timeline, reader.Events, planNodes)
        {
            Allocation = memory,
            AddressMap = reader.HasEntry(AddressMapEntry) && reader.ReadBinary(AddressMapEntry, AddressMap.IsSupported)
                ? reader.ReadBinary(AddressMapEntry, r => AddressMap.Read(r, timeline.GetNode))
                : AddressMap.Empty,
            MemoryObjectSnapshot = reader.HasEntry(MemoryObjectsEntry) && reader.ReadBinary(MemoryObjectsEntry, MemoryObjectMap.IsSupported)
                ? reader.ReadBinary(MemoryObjectsEntry, MemoryObjectMap.ReadObjects)
                : new Dictionary<ulong, MemoryObjectInfo>(),
            ObjectPool = reader.HasEntry(ObjectPoolSnapshotEntry)
                ? reader.ReadBinary(ObjectPoolSnapshotEntry, ObjectPoolSnapshot.Read)
                : ObjectPoolSnapshot.Empty,
            BufferPool = reader.HasEntry(BufferPoolEntry) && reader.ReadBinary(BufferPoolEntry, TraceBufferPool.IsSupported)
                ? reader.ReadBinary(BufferPoolEntry, TraceBufferPool.Read)
                : TraceBufferPool.Empty
        };

        var result = builder.Build() with
        {
            MemoryHistory = reader.HasEntry(MemoryHistoryEntry) && reader.ReadBinary(MemoryHistoryEntry, MemoryHistoryStore.IsSupported)
                ? reader.ReadBinary(MemoryHistoryEntry, r => MemoryHistoryStore.Read(r, timeline))
                : MemoryHistoryStore.Empty,
            PageReads = reader.HasEntry(PageReadsEntry) && reader.ReadBinary(PageReadsEntry, TracePageReadList.IsSupported)
                ? reader.ReadBinary(PageReadsEntry, TracePageReadList.Read)
                : TracePageReadList.Empty
        };

        result = result.WithPageAccesses();

        callStore.ReleaseColumns();

        return result;
    }
}
