using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.Export;

public static class RunExport
{
    public const string Extension = ".ivtrace";

    internal const int FormatVersion = 1;

    internal const uint BinaryMagic = 0x54465649;

    internal const string ManifestEntry = "manifest.json";

    internal const string QueryEntry = "query.sql";

    internal const string MessagesEntry = "messages.txt";

    internal const string FramesEntry = "frames.jsonl";

    internal const string EventsEntry = "events.jsonl";

    internal const string LogRecordsEntry = "log-records.jsonl";

    internal const string CallLogEntry = "fulltrace/calllog.bin";

    internal const string TimelineEntry = "fulltrace/timeline.bin";

    internal const string EventLinksEntry = "fulltrace/eventlinks.bin";

    public static Task WriteAsync(Stream stream, RunExportContent content, CancellationToken cancellationToken)
        => Task.Run(() => RunExportWriter.Write(stream, content, cancellationToken), cancellationToken);

    public static Task<RunExportContent> ReadAsync(Stream stream,
                                                    Func<long, AllocationUnit?> allocationUnitOf,
                                                    CancellationToken cancellationToken)
        => Task.Run(() => RunExportReader.Read(stream, allocationUnitOf, cancellationToken), cancellationToken);
}
