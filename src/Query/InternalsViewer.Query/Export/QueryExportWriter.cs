using System.IO.Compression;
using System.Text.Json;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Results;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Export;

public sealed class QueryExportWriter
{
    private const int BufferSize = 1 << 16;

    private QueryExportWriter(ZipArchive zip)
    {
        Zip = zip;
    }

    private ZipArchive Zip { get; }

    public void WriteBinary(string name, CompressionLevel compression, Action<BinaryWriter> write)
    {
        using var stream = new BufferedStream(Zip.CreateEntry(name, compression).Open(), BufferSize);

        using var writer = new BinaryWriter(stream);

        writer.Write(QueryExport.BinaryMagic);

        writer.Write(QueryExport.FormatVersion);

        write(writer);
    }

    public void WriteLines<T>(string name, IEnumerable<T> items) => WriteLines(Zip, name, items, ExportPackage.Plain);

    internal static void Write(Stream stream,
                               QueryExportContent content,
                               IReadOnlyList<IQueryExportSection> sections,
                               CancellationToken cancellationToken)
    {
        var references = new ExportReferences(_ => null);

        Collect(content.Result, references);

        cancellationToken.ThrowIfCancellationRequested();

        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        WriteText(zip, QueryExport.QueryEntry, content.SqlText);

        WriteText(zip, QueryExport.MessagesEntry, string.Join(Environment.NewLine, content.Messages));

        var plans = WritePlans(zip, content.Result.ExecutionPlans);

        cancellationToken.ThrowIfCancellationRequested();

        var options = ExportPackage.For(references);

        WriteLines(zip, QueryExport.LogRecordsEntry, references.LogRecords.InOrder(), options);

        WriteLines(zip, QueryExport.EventsEntry, references.Events.InOrder(), options);

        var writer = new QueryExportWriter(zip);

        foreach (var section in sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            section.Write(writer, content);
        }

        WriteJson(zip, QueryExport.ManifestEntry, ManifestOf(content, plans, references), ExportPackage.Indented);
    }

    private static void Collect(QueryResult result, ExportReferences references)
    {
        references.TopLevel.UnionWith(result.EngineEvents);

        var added = new List<EngineEvent>();

        foreach (var engineEvent in result.EngineEvents)
        {
            AddEvent(engineEvent, references, added);
        }

        foreach (var logRecord in result.LogRecords.Concat(added.SelectMany(e => ExportPackage.ReferencesOf(e).OfType<LogRecord>())))
        {
            references.LogRecords.Add(logRecord);
        }
    }

    private static void AddEvent(EngineEvent root, ExportReferences references, List<EngineEvent> added)
    {
        var stack = new Stack<(EngineEvent Event, bool Expanded)>();

        var visiting = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        stack.Push((root, false));

        while (stack.Count > 0)
        {
            var (current, expanded) = stack.Pop();

            if (references.Events.Contains(current))
            {
                continue;
            }

            if (expanded)
            {
                references.Events.Add(current);

                added.Add(current);

                continue;
            }

            if (!visiting.Add(current))
            {
                continue;
            }

            stack.Push((current, true));

            foreach (var dependency in ExportPackage.ReferencesOf(current).OfType<EngineEvent>())
            {
                if (!references.Events.Contains(dependency))
                {
                    stack.Push((dependency, false));
                }
            }
        }
    }

    private static List<ExportPlan> WritePlans(ZipArchive zip, IReadOnlyList<ExecutionPlan> plans)
    {
        var exported = new List<ExportPlan>(plans.Count);

        for (var index = 0; index < plans.Count; index++)
        {
            var plan = plans[index];

            var file = $"plans/plan-{index}.xml";

            WriteText(zip, file, plan.Xml ?? string.Empty);

            var batches = plan.NodesById.Where(n => n.Value.BatchInfo is not null).ToDictionary(n => n.Key, n => n.Value.BatchInfo!);

            string? batchFile = null;

            if (batches.Count > 0)
            {
                batchFile = $"plans/plan-{index}.batch.json";

                WriteJson(zip, batchFile, batches, ExportPackage.Indented);
            }

            exported.Add(new ExportPlan(index, plan.PlanHandleId, plan.IsInternalPlan, file, batchFile));
        }

        return exported;
    }

    private static ExportManifest ManifestOf(QueryExportContent content,
                                              List<ExportPlan> plans,
                                              ExportReferences references)
    {
        var result = content.Result;

        return new ExportManifest
        {
            FormatVersion = QueryExport.FormatVersion,
            ExportedAt = DateTimeOffset.Now,
            ServerName = content.ServerName,
            DatabaseName = content.DatabaseName,
            SessionId = result.SessionId,
            IsSuccess = result.IsSuccess,
            Message = result.Message,
            RowCount = result.RowCount,
            CropStartUs = result.CropStartUs,
            CropEndUs = result.CropEndUs,
            QueryOptions = content.QueryOptions,
            EventOptions = content.EventOptions,
            ResultSets = [.. result.ResultSets.Select(s => new ExportResultSet([.. s.Columns.Select(ColumnOf)]))],
            Plans = plans,
            ResultLogRecords = [.. result.LogRecords.Select(references.LogRecords.IdOf)],
            EventCount = references.Events.Count,
            LogRecordCount = references.LogRecords.Count
        };
    }

    private static ExportColumn ColumnOf(ResultColumn column) => new(column.Ordinal,
                                                                      column.Name,
                                                                      column.ClrType.FullName ?? column.ClrType.Name,
                                                                      column.IsNullable,
                                                                      column.TypeName,
                                                                      column.Detail,
                                                                      column.Width,
                                                                      column.Alignment);

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open());

        writer.Write(text);
    }

    private static void WriteJson<T>(ZipArchive zip, string name, T value, JsonSerializerOptions options)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();

        JsonSerializer.Serialize(stream, value, options);
    }

    private static void WriteLines<T>(ZipArchive zip, string name, IEnumerable<T> items, JsonSerializerOptions options)
    {
        using var stream = new BufferedStream(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), BufferSize);

        foreach (var item in items)
        {
            JsonSerializer.Serialize(stream, item, options);

            stream.WriteByte((byte)'\n');
        }
    }
}
