using System.IO.Compression;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.FullTrace.Export;

internal sealed class TraceExportSection : IQueryExportSection
{
    internal const string CallTreeName = "fulltrace";

    internal const string CallStoreEntry = "fulltrace/calllog.bin";

    internal const string TimelineEntry = "fulltrace/timeline.bin";

    internal const string MemoryMapEntry = "fulltrace/memorymap.bin";

    internal const string ObjectPoolSnapshotEntry = "fulltrace/objectpool.bin";

    internal const string OutputStructuresEntry = "fulltrace/output.bin";

    internal const string PlanNodesEntry = "fulltrace/plannodes.bin";

    internal const string TeardownsEntry = "fulltrace/teardowns.bin";

    public bool CanExport(QueryExportContent content) => content.ExtraOf<TraceResult>() is not null;

    public void Collect(QueryExportCollector collector, QueryExportContent content)
    {
        var fullTrace = content.ExtraOf<TraceResult>()!;

        collector.AddTree(CallTreeName, fullTrace.CallStack);

        collector.AddReferencedNodes(fullTrace.Timeline.MappedNodes);
    }

    public void Write(QueryExportWriter writer, QueryExportContent content)
    {
        var fullTrace = content.ExtraOf<TraceResult>()!;

        writer.WriteBinary(CallStoreEntry, CompressionLevel.NoCompression, fullTrace.CallStore.Write);

        writer.WriteBinary(TimelineEntry,
                           CompressionLevel.Fastest,
                           w =>
                           {
                               fullTrace.Timeline.Write(w, writer.IdOf);

                               fullTrace.Memory.Write(w);
                           });

        writer.WriteBinary(MemoryMapEntry, CompressionLevel.Fastest, fullTrace.MemoryMap.Write);

        writer.WriteBinary(ObjectPoolSnapshotEntry, CompressionLevel.Fastest, fullTrace.ObjectPool.Write);

        writer.WriteBinary(OutputStructuresEntry, CompressionLevel.Fastest, w => TraceOutputStructure.Write(w, fullTrace.OutputStructures));

        writer.WriteBinary(PlanNodesEntry, CompressionLevel.Fastest, w => IteratorPlanNode.Write(w, fullTrace.PlanNodes));

        writer.WriteBinary(TeardownsEntry, CompressionLevel.Fastest, w => QueryTeardown.Write(w, fullTrace.Teardowns));
    }

    public IRunExtra? Read(QueryExportReader reader)
    {
        if (reader.CallTree(CallTreeName) is not { } tree
            || !reader.ReadBinary(CallStoreEntry, TraceCallStore.IsCurrent)
            || !reader.ReadBinary(TimelineEntry, TraceTimeline.IsCurrent))
        {
            return null;
        }

        var nodes = reader.Nodes;

        var callStore = reader.ReadBinary(CallStoreEntry, TraceCallStore.Read);

        var (timeline, memory) = reader.ReadBinary(TimelineEntry, r => (TraceTimeline.Read(r, nodes), MemoryIndex.Read(r)));

        callStore.MapNodes(timeline.MappedNodes);

        List<TraceOutputStructure> outputStructures = reader.HasEntry(OutputStructuresEntry)
            ? reader.ReadBinary(OutputStructuresEntry, TraceOutputStructure.Read)
            : [];

        IReadOnlyList<IteratorPlanNode> planNodes = reader.HasEntry(PlanNodesEntry)
            ? reader.ReadBinary(PlanNodesEntry, IteratorPlanNode.Read)
            : [];

        IReadOnlyList<QueryTeardown> teardowns = reader.HasEntry(TeardownsEntry)
            ? reader.ReadBinary(TeardownsEntry, QueryTeardown.Read)
            : [];

        var builder = new TraceResultBuilder(tree, callStore, timeline, reader.Events, planNodes, teardowns)
        {
            Memory = memory,
            MemoryMap = reader.HasEntry(MemoryMapEntry) && reader.ReadBinary(MemoryMapEntry, MemoryMap.IsSupported)
                ? reader.ReadBinary(MemoryMapEntry, r => MemoryMap.Read(r, timeline.NodeOf))
                : MemoryMap.Empty,
            ObjectPool = reader.HasEntry(ObjectPoolSnapshotEntry)
                ? reader.ReadBinary(ObjectPoolSnapshotEntry, ObjectPoolSnapshot.Read)
                : ObjectPoolSnapshot.Empty
        };

        return builder.Build(outputStructures);
    }
}
