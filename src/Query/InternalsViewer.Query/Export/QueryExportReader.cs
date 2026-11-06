using System.IO.Compression;
using System.Text.Json;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Parsers;
using InternalsViewer.Query.Results;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Export;

public sealed class QueryExportReader
{
    private const int BufferSize = 1 << 16;

    private QueryExportReader(ZipArchive zip, IReadOnlyList<EngineEvent> events)
    {
        Zip = zip;
        Events = events;
    }

    public IReadOnlyList<EngineEvent> Events { get; }

    private ZipArchive Zip { get; }

    public bool HasEntry(string name) => Zip.GetEntry(name) is not null;

    public T ReadBinary<T>(string name, Func<BinaryReader, T> read)
    {
        var entry = Zip.GetEntry(name) ?? throw new InvalidDataException($"The run file has no {name}");

        using var reader = new BinaryReader(new BufferedStream(entry.Open(), BufferSize));

        if (reader.ReadUInt32() != QueryExport.BinaryMagic)
        {
            throw new InvalidDataException($"{name} is not an Internals Viewer binary section");
        }

        var version = reader.ReadInt32();

        if (version != QueryExport.FormatVersion)
        {
            throw new InvalidDataException($"{name} was exported in format {version}, this version of Internals Viewer reads format "
                                           + $"{QueryExport.FormatVersion}");
        }

        return read(reader);
    }

    public IEnumerable<T> ReadLines<T>(string name) => ReadLines<T>(Zip, name, ExportPackage.Plain);

    internal static QueryExportContent Read(Stream stream,
                                            Func<long, AllocationUnit?> allocationUnitOf,
                                            IReadOnlyList<IQueryExportSection> sections,
                                            CancellationToken cancellationToken)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        var manifest = ReadJson<ExportManifest>(zip, QueryExport.ManifestEntry, ExportPackage.Plain);

        if (manifest.FormatVersion != QueryExport.FormatVersion)
        {
            throw new InvalidDataException($"The run was exported in format {manifest.FormatVersion}, this version of Internals Viewer "
                                           + $"reads format {QueryExport.FormatVersion}");
        }

        var references = new ExportReferences(allocationUnitOf);

        var options = ExportPackage.For(references);

        var logRecordCount = ReadLines<LogRecord>(zip, QueryExport.LogRecordsEntry, options).Count();

        var eventCount = ReadLines<EngineEvent>(zip, QueryExport.EventsEntry, options).Count();

        if (logRecordCount != manifest.LogRecordCount || eventCount != manifest.EventCount)
        {
            throw new InvalidDataException($"The run file is incomplete: {eventCount:N0} of {manifest.EventCount:N0} events and "
                                           + $"{logRecordCount:N0} of {manifest.LogRecordCount:N0} log records could be read");
        }

        cancellationToken.ThrowIfCancellationRequested();

        List<QueryResultSet> resultSets = [.. manifest.ResultSets.Select(ResultSetOf)];

        var plans = ReadPlans(zip, manifest.Plans);

        ExpressionCatalog.Populate(plans, resultSets);

        var result = new QueryResult
        {
            SessionId = manifest.SessionId,
            IsSuccess = manifest.IsSuccess,
            EngineEvents = references.TopLevelOrder,
            ExecutionPlans = plans,
            ResultSets = resultSets,
            Message = manifest.Message,
            RowCount = manifest.RowCount,
            CropStartUs = manifest.CropStartUs,
            CropEndUs = manifest.CropEndUs,
            LogRecords = [.. manifest.ResultLogRecords.Select(id => references.LogRecords[id])]
        };

        var reader = new QueryExportReader(zip, result.EngineEvents);

        var extras = new List<IRunExtra>();

        foreach (var section in sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (section.Read(reader) is { } extra)
            {
                extras.Add(extra);
            }
        }

        return new QueryExportContent
        {
            ServerName = manifest.ServerName,
            DatabaseName = manifest.DatabaseName,
            SqlText = ReadText(zip, QueryExport.QueryEntry),
            QueryOptions = manifest.QueryOptions,
            EventOptions = manifest.EventOptions,
            Messages = ReadText(zip, QueryExport.MessagesEntry).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
            Result = result,
            Extras = extras,
            ExportedAt = manifest.ExportedAt
        };
    }

    private static List<ExecutionPlan> ReadPlans(ZipArchive zip, IReadOnlyList<ExportPlan> exported)
    {
        var plans = new List<ExecutionPlan>(exported.Count);

        foreach (var plan in exported.OrderBy(p => p.Index))
        {
            var xml = ReadText(zip, plan.File);

            if (xml.Length == 0)
            {
                continue;
            }

            var parsed = ExecutionPlanParser.Parse(xml, plan.PlanHandleId);

            parsed.IsInternalPlan = plan.IsInternalPlan;

            if (plan.BatchFile is { } batchFile)
            {
                foreach (var (nodeId, info) in ReadJson<Dictionary<int, BatchInfo>>(zip, batchFile, ExportPackage.Populating))
                {
                    if (parsed.NodesById.TryGetValue(nodeId, out var node))
                    {
                        node.BatchInfo = info;
                    }
                }
            }

            plans.Add(parsed);
        }

        return plans;
    }

    private static QueryResultSet ResultSetOf(ExportResultSet resultSet) => new()
    {
        Columns =
        [
            .. resultSet.Columns.Select(c => new ResultColumn(c.Ordinal, c.Name, Type.GetType(c.ClrType) ?? typeof(object), c.IsNullable)
            {
                TypeName = c.TypeName,
                Detail = c.Detail,
                Width = c.Width,
                Alignment = c.Alignment
            })
        ]
    };

    private static string ReadText(ZipArchive zip, string name)
    {
        if (zip.GetEntry(name) is not { } entry)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(entry.Open());

        return reader.ReadToEnd();
    }

    private static T ReadJson<T>(ZipArchive zip, string name, JsonSerializerOptions options)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"The run file has no {name}");

        using var stream = entry.Open();

        return JsonSerializer.Deserialize<T>(stream, options) ?? throw new InvalidDataException($"{name} is empty");
    }

    private static IEnumerable<T> ReadLines<T>(ZipArchive zip, string name, JsonSerializerOptions options)
    {
        if (zip.GetEntry(name) is not { } entry)
        {
            yield break;
        }

        using var reader = new StreamReader(new BufferedStream(entry.Open(), BufferSize));

        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                yield return JsonSerializer.Deserialize<T>(line, options)!;
            }
        }
    }
}
