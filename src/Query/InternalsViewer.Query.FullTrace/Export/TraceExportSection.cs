using System.IO.Compression;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.Arguments;
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

    internal const string EvaluationsEntry = "fulltrace/evaluations.bin";

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

        writer.WriteBinary(MemoryMapEntry, CompressionLevel.Fastest, fullTrace.MemoryMap.Write);

        writer.WriteBinary(ObjectPoolSnapshotEntry, CompressionLevel.Fastest, fullTrace.ObjectPool.Write);

        writer.WriteBinary(OutputStructuresEntry, CompressionLevel.Fastest, w => TraceOutputStructure.Write(w, fullTrace.OutputStructures));

        writer.WriteBinary(PlanNodesEntry, CompressionLevel.Fastest, w => IteratorPlanNode.Write(w, fullTrace.PlanNodes));

        writer.WriteBinary(TeardownsEntry, CompressionLevel.Fastest, w => QueryTeardown.Write(w, fullTrace.Teardowns));

        writer.WriteBinary(EvaluationsEntry, CompressionLevel.Fastest, fullTrace.Evaluations.Write);
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

        List<TraceOutputStructure> outputStructures = reader.HasEntry(OutputStructuresEntry)
            ? reader.ReadBinary(OutputStructuresEntry, TraceOutputStructure.Read)
            : [];

        IReadOnlyList<IteratorPlanNode> planNodes = reader.HasEntry(PlanNodesEntry)
            ? reader.ReadBinary(PlanNodesEntry, IteratorPlanNode.Read)
            : [];

        IReadOnlyList<QueryTeardown> teardowns = reader.HasEntry(TeardownsEntry)
            ? reader.ReadBinary(TeardownsEntry, QueryTeardown.Read)
            : [];

        timeline.SetLifetimes(IteratorLifetimeBuilder.Build(timeline, [.. events.OfType<ExecutionOperatorEvent>()], planNodes, teardowns));

        return new TraceResult(tree, callStore, timeline)
        {
            PlanLink = IteratorPlanLinker.Link(planNodes, events),
            PlanNodes = planNodes,
            Teardowns = teardowns,
            HoBtAccess = HoBtAccessMap.Build(callStore, timeline.MappedNodes),
            MemoryMap = reader.HasEntry(MemoryMapEntry)
                ? reader.ReadBinary(MemoryMapEntry, r => MemoryMap.Read(r, timeline.NodeOf))
                : MemoryMap.Empty,
            ObjectPool = reader.HasEntry(ObjectPoolSnapshotEntry)
                ? reader.ReadBinary(ObjectPoolSnapshotEntry, ObjectPoolSnapshot.Read)
                : ObjectPoolSnapshot.Empty,
            OutputStructures = outputStructures,
            RowReturnMarks = TraceRowReturnMarks.Build(timeline, callStore, TraceOutputLineage.GetSources(timeline, outputStructures)),
            Reads = TraceReads.Build(timeline, callStore),
            Evaluations = reader.HasEntry(EvaluationsEntry)
                ? TraceEvaluations.Build(timeline, callStore, reader.ReadBinary(EvaluationsEntry, TraceEvaluations.ReadCaptures))
                : TraceEvaluations.Empty
        };
    }
}
