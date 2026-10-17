using System.IO.Compression;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.Memory;

namespace InternalsViewer.Query.FullTrace.Export;

internal sealed class TraceExportSection : IQueryExportSection
{
    internal const string CallTreeName = "fulltrace";

    internal const string CallStoreEntry = "fulltrace/calllog.bin";

    internal const string TimelineEntry = "fulltrace/timeline.bin";

    internal const string EventLinksEntry = "fulltrace/eventlinks.bin";

    internal const string MemoryMapEntry = "fulltrace/memorymap.bin";

    internal const string RowBuffersEntry = "fulltrace/rowbuffers.bin";

    internal const string BatchesEntry = "fulltrace/batches.bin";

    internal const string ObjectPoolSnapshotEntry = "fulltrace/objectpool.bin";

    internal const string OutputStructuresEntry = "fulltrace/output.bin";

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

        writer.WriteBinary(TimelineEntry, CompressionLevel.Fastest, w => fullTrace.Timeline.Write(w, writer.IdOf));

        writer.WriteBinary(EventLinksEntry, CompressionLevel.Fastest, w => fullTrace.Timeline.WriteEventLinks(w, writer.IdOf));

        writer.WriteBinary(MemoryMapEntry, CompressionLevel.Fastest, fullTrace.MemoryMap.Write);

        writer.WriteBinary(RowBuffersEntry, CompressionLevel.Fastest, fullTrace.RowBuffers.Write);

        writer.WriteBinary(BatchesEntry, CompressionLevel.Fastest, fullTrace.Batches.Write);

        writer.WriteBinary(ObjectPoolSnapshotEntry, CompressionLevel.Fastest, fullTrace.ObjectPool.Write);

        writer.WriteBinary(OutputStructuresEntry, CompressionLevel.Fastest, w => TraceOutputStructure.Write(w, fullTrace.OutputStructures));
    }

    public IRunExtra? Read(QueryExportReader reader)
    {
        if (reader.CallTree(CallTreeName) is not { } tree || !reader.ReadBinary(CallStoreEntry, TraceCallStore.IsCurrent))
        {
            return null;
        }

        var nodes = reader.Nodes;

        var events = reader.Events;

        var callStore = reader.ReadBinary(CallStoreEntry, TraceCallStore.Read);

        var timeline = reader.ReadBinary(TimelineEntry, r => TraceTimeline.Read(r, nodes));

        callStore.MapNodes(timeline.MappedNodes);

        timeline.SetCalls(callStore);

        var rowBuffers = reader.HasEntry(RowBuffersEntry) ? reader.ReadBinary(RowBuffersEntry, RowBufferStore.Read) : RowBufferStore.Empty;

        var batches = reader.HasEntry(BatchesEntry) ? reader.ReadBinary(BatchesEntry, BatchStore.Read) : BatchStore.Empty;

        List<TraceOutputStructure> outputStructures = reader.HasEntry(OutputStructuresEntry)
            ? reader.ReadBinary(OutputStructuresEntry, TraceOutputStructure.Read)
            : [];

        rowBuffers.AnnotateCallStackNodes(callStore);

        batches.AnnotateNodes(callStore);

        timeline.SetLifetimes(IteratorLifetimeBuilder.Build(timeline, [.. events.OfType<ExecutionOperatorEvent>()]));

        if (reader.HasEntry(EventLinksEntry))
        {
            timeline.SetEventLinks(reader.ReadBinary(EventLinksEntry, r => TraceTimeline.ReadEventLinks(r, nodes, events)));
        }

        return new TraceResult(tree, callStore, timeline)
        {
            PlanLink = IteratorPlanLinker.Link(callStore, timeline.MappedNodes, events),
            HoBtAccess = HoBtAccessMap.Build(callStore, timeline.MappedNodes),
            MemoryMap = reader.HasEntry(MemoryMapEntry)
                ? reader.ReadBinary(MemoryMapEntry, r => MemoryMap.Read(r, timeline.NodeOf))
                : MemoryMap.Empty,
            ObjectPool = reader.HasEntry(ObjectPoolSnapshotEntry)
                ? reader.ReadBinary(ObjectPoolSnapshotEntry, ObjectPoolSnapshot.Read)
                : ObjectPoolSnapshot.Empty,
            RowBuffers = rowBuffers,
            Batches = batches,
            OutputStructures = outputStructures,
            RowBufferMarks = TraceRowBufferMarks.Build(timeline, rowBuffers, batches),
            RowReturnMarks = TraceRowReturnMarks.Build(timeline,
                                                       callStore,
                                                       TraceOutputLineage.Build(timeline, outputStructures, rowBuffers))
        };
    }
}
