using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Query.Export;

public sealed class RunExport(IEnumerable<IQueryExportSection> sections)
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

    private IReadOnlyList<IQueryExportSection> Sections { get; } = [.. sections];

    public Task WriteAsync(Stream stream, RunExportContent content, CancellationToken cancellationToken)
    {
        IQueryExportSection[] exporting = [.. Sections.Where(s => s.CanExport(content))];

        return Task.Run(() => RunExportWriter.Write(stream, content, exporting, cancellationToken), cancellationToken);
    }

    public Task<RunExportContent> ReadAsync(Stream stream,
                                            Func<long, AllocationUnit?> allocationUnitOf,
                                            CancellationToken cancellationToken)
        => Task.Run(() => RunExportReader.Read(stream, allocationUnitOf, Sections, cancellationToken), cancellationToken);
}
