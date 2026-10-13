using System.IO.Compression;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Export;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Export;

internal sealed class TraceExportSection : IQueryExportSection
{
    internal const string CallTreeName = "fulltrace";

    internal const string CallLogEntry = "fulltrace/calllog.bin";

    internal const string TimelineEntry = "fulltrace/timeline.bin";

    internal const string EventLinksEntry = "fulltrace/eventlinks.bin";

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

        writer.WriteBinary(CallLogEntry, CompressionLevel.NoCompression, fullTrace.CallLog.Write);

        writer.WriteBinary(TimelineEntry, CompressionLevel.Fastest, w => fullTrace.Timeline.Write(w, writer.IdOf));

        writer.WriteBinary(EventLinksEntry, CompressionLevel.Fastest, w => fullTrace.Timeline.WriteEventLinks(w, writer.IdOf));
    }

    public IRunExtra? Read(QueryExportReader reader)
    {
        if (reader.CallTree(CallTreeName) is not { } tree)
        {
            return null;
        }

        var nodes = reader.Nodes;

        var events = reader.Events;

        var callLog = reader.ReadBinary(CallLogEntry, TraceCallLog.Read);

        var timeline = reader.ReadBinary(TimelineEntry, r => TraceTimeline.Read(r, nodes));

        callLog.MapNodes(timeline.MappedNodes);

        timeline.SetLifetimes(IteratorLifetimeBuilder.Build(timeline, [.. events.OfType<ExecutionOperatorEvent>()]));

        if (reader.HasEntry(EventLinksEntry))
        {
            timeline.SetEventLinks(reader.ReadBinary(EventLinksEntry, r => TraceTimeline.ReadEventLinks(r, nodes, events)));
        }

        return new TraceResult(tree, callLog, timeline) { PlanLink = IteratorPlanLinker.Link(callLog, timeline.MappedNodes, events) };
    }
}
