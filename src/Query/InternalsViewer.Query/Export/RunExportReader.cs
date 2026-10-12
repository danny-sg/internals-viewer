using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Parsers;
using InternalsViewer.Query.Results;
using InternalsViewer.TransactionLog.LogRecords;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.Export;

public sealed class RunExportReader
{
    private const int BufferSize = 1 << 16;

    private RunExportReader(ZipArchive zip,
                            ExportReferences references,
                            List<(ExportCallTree Exported, CallStackTree? Tree)> trees,
                            IReadOnlyList<EngineEvent> events)
    {
        Zip = zip;
        References = references;
        Trees = trees;
        Events = events;
    }

    public IReadOnlyList<EngineEvent> Events { get; }

    public IReadOnlyList<CallStackNode> Nodes => field ??= [.. References.Nodes.InOrder()];

    private ZipArchive Zip { get; }

    private ExportReferences References { get; }

    private List<(ExportCallTree Exported, CallStackTree? Tree)> Trees { get; }

    public CallStackTree? CallTree(string name) => Trees.FirstOrDefault(t => t.Exported.Name == name).Tree;

    public bool HasEntry(string name) => Zip.GetEntry(name) is not null;

    public T ReadBinary<T>(string name, Func<BinaryReader, T> read)
    {
        var entry = Zip.GetEntry(name) ?? throw new InvalidDataException($"The run file has no {name}");

        using var reader = new BinaryReader(new BufferedStream(entry.Open(), BufferSize));

        if (reader.ReadUInt32() != RunExport.BinaryMagic)
        {
            throw new InvalidDataException($"{name} is not an Internals Viewer binary section");
        }

        var version = reader.ReadInt32();

        if (version > RunExport.FormatVersion)
        {
            throw new InvalidDataException($"{name} was exported by a newer version of Internals Viewer (format {version})");
        }

        return read(reader);
    }

    internal static RunExportContent Read(Stream stream,
                                          Func<long, AllocationUnit?> allocationUnitOf,
                                          IReadOnlyList<IRunExportSection> sections,
                                          CancellationToken cancellationToken)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        var manifest = ReadJson<ExportManifest>(zip, RunExport.ManifestEntry, ExportPackage.Plain);

        if (manifest.FormatVersion > RunExport.FormatVersion)
        {
            throw new InvalidDataException($"The run was exported by a newer version of Internals Viewer (format {manifest.FormatVersion})");
        }

        var references = new ExportReferences(allocationUnitOf);

        foreach (var frame in ReadLines<ExportedFrame>(zip, RunExport.FramesEntry, ExportPackage.Plain))
        {
            references.Frames.Register(frame.Id, FrameOf(frame));
        }

        var nodeEvents = new List<(CallStackNode Node, int[] Events)>();

        var trees = manifest.CallTrees.Select(t => (Exported: t, Tree: ReadTree(zip, t, references, nodeEvents))).ToList();

        cancellationToken.ThrowIfCancellationRequested();

        var options = ExportPackage.For(references);

        var logRecordCount = ReadLines<LogRecord>(zip, RunExport.LogRecordsEntry, options).Count();

        var eventCount = ReadLines<EngineEvent>(zip, RunExport.EventsEntry, options).Count();

        if (logRecordCount != manifest.LogRecordCount || eventCount != manifest.EventCount)
        {
            throw new InvalidDataException($"The run file is incomplete: {eventCount:N0} of {manifest.EventCount:N0} events and "
                                           + $"{logRecordCount:N0} of {manifest.LogRecordCount:N0} log records could be read");
        }

        foreach (var (node, events) in nodeEvents)
        {
            node.Events.AddRange(events.Select(id => references.Events[id]));
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
            CallStackTree = trees.FirstOrDefault(t => t.Exported.IsResultTree).Tree,
            ResultSets = resultSets,
            Message = manifest.Message,
            RowCount = manifest.RowCount,
            CropStartUs = manifest.CropStartUs,
            CropEndUs = manifest.CropEndUs,
            LogRecords = [.. manifest.ResultLogRecords.Select(id => references.LogRecords[id])]
        };

        var reader = new RunExportReader(zip, references, trees, result.EngineEvents);

        var extras = new List<IRunExtra>();

        foreach (var section in sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (section.Read(reader) is { } extra)
            {
                extras.Add(extra);
            }
        }

        OperatorEntryLabels.Apply(result.EngineEvents);

        return new RunExportContent
        {
            ServerName = manifest.ServerName,
            DatabaseName = manifest.DatabaseName,
            SqlText = ReadText(zip, RunExport.QueryEntry),
            QueryOptions = manifest.QueryOptions,
            EventOptions = manifest.EventOptions,
            Messages = ReadText(zip, RunExport.MessagesEntry).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
            Result = result,
            Extras = extras,
            ExportedAt = manifest.ExportedAt
        };
    }

    private static CallStackTree? ReadTree(ZipArchive zip,
                                           ExportCallTree exported,
                                           ExportReferences references,
                                           List<(CallStackNode Node, int[] Events)> nodeEvents)
    {
        var tree = exported.IsDetached ? null : new CallStackTree { ActivityFromTrace = exported.ActivityFromTrace };

        tree?.RestoreActivity(exported.ActivityMinUs, exported.ActivityMaxUs, exported.ActivityBuckets, exported.ActivityBusiest);

        foreach (var line in ReadLines<ExportedNode>(zip, exported.File, ExportPackage.Plain))
        {
            var isRoot = tree is not null && line.Id == exported.RootId;

            var node = isRoot
                ? tree!.Root
                : new CallStackNode
                {
                    Frame = line.Frame is { } frame ? references.Frames[frame] : null,
                    Key = line.Key,
                    Order = line.Order
                };

            if (line.Parent is { } parentId && !isRoot)
            {
                var parent = references.Nodes[parentId];

                node.Parent = parent;

                if (!line.Detached)
                {
                    parent.Children[node.Key] = node;
                }
            }

            node.Rvas.UnionWith(line.Rvas ?? []);

            node.Calls = line.Calls;

            node.CallActivity = line.CallActivity ?? [];

            node.ActivityCounts = line.ActivityCounts ?? [];

            node.AllocatedBytes = line.AllocatedBytes;

            node.Allocations = line.Allocations;

            node.Locators = LocatorsOf(line.Locators);

            references.Nodes.Register(line.Id, node);

            if (line.Events is { Length: > 0 } events)
            {
                nodeEvents.Add((node, events));
            }
        }

        return tree;
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

    private static CallstackFrame FrameOf(ExportedFrame frame) => new()
    {
        Module = frame.Module,
        Address = frame.Address,
        Pdb = frame.Pdb,
        Guid = frame.Guid,
        Age = frame.Age,
        Rva = frame.Rva,
        Instance = frame.Instance,
        Signature = frame.Signature,
        Resolved = frame.Symbol is { } symbol ? ResolvedCallstackFrameParser.Parse(frame.SymbolModule ?? frame.Module, symbol) : null
    };

    private static LocatorSet? LocatorsOf(ExportedLocators? exported)
    {
        if (exported is null)
        {
            return null;
        }

        var locators = new LocatorSet();

        foreach (var page in exported.Pages ?? [])
        {
            locators.Add(new Locator(0, LocatorKind.Page, LocatorValue(page, 2)));
        }

        foreach (var row in exported.Rows ?? [])
        {
            locators.Add(new Locator(0, LocatorKind.Row, LocatorValue(row, 3)));
        }

        foreach (var columnStoreObject in exported.Objects ?? [])
        {
            locators.Add(new Locator(0, LocatorKind.ColumnStoreObject, columnStoreObject[0], columnStoreObject[1]));
        }

        return locators;
    }

    private static ulong LocatorValue(string text, int parts)
    {
        var values = text.Split(':');

        if (values.Length != parts)
        {
            throw new InvalidDataException($"'{text}' is not a page or row id");
        }

        var fileId = (ushort)short.Parse(values[0], CultureInfo.InvariantCulture);

        var pageId = uint.Parse(values[1], CultureInfo.InvariantCulture);

        var slot = parts == 3 ? ulong.Parse(values[2], CultureInfo.InvariantCulture) : 0;

        return (slot << 48) | ((ulong)fileId << 32) | pageId;
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
