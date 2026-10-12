using System.IO.Compression;
using System.Text.Json;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Results;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Export;

internal static class RunExportWriter
{
    private const int BufferSize = 1 << 16;

    private const string EventsTree = "events";

    private const string FullTraceTree = "fulltrace";

    private const string DetachedTree = "detached";

    public static void Write(Stream stream, RunExportContent content, CancellationToken cancellationToken)
    {
        var references = new ExportReferences(_ => null);

        var trees = Collect(content, references);

        cancellationToken.ThrowIfCancellationRequested();

        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        WriteText(zip, RunExport.QueryEntry, content.SqlText);

        WriteText(zip, RunExport.MessagesEntry, string.Join(Environment.NewLine, content.Messages));

        var plans = WritePlans(zip, content.Result.ExecutionPlans);

        WriteLines(zip, RunExport.FramesEntry, references.Frames.InOrder().Select(f => FrameOf(f, references)), ExportPackage.Plain);

        foreach (var (tree, roots) in trees)
        {
            WriteLines(zip, tree.File, roots.SelectMany(PreOrder).Select(n => NodeOf(n, references)), ExportPackage.Plain);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var options = ExportPackage.For(references);

        WriteLines(zip, RunExport.LogRecordsEntry, references.LogRecords.InOrder(), options);

        WriteLines(zip, RunExport.EventsEntry, references.Events.InOrder(), options);

        if (content.FullTrace is { } fullTrace)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WriteBinary(zip, RunExport.CallLogEntry, CompressionLevel.NoCompression, fullTrace.CallLog.Write);

            WriteBinary(zip, RunExport.TimelineEntry, CompressionLevel.Fastest, w => fullTrace.Timeline.Write(w, references.Nodes.IdOf));
        }

        WriteJson(zip, RunExport.ManifestEntry, ManifestOf(content, trees, plans, references), ExportPackage.Indented);
    }

    private static List<(ExportCallTree Tree, IReadOnlyList<CallStackNode> Roots)> Collect(RunExportContent content,
                                                                                         ExportReferences references)
    {
        var result = content.Result;

        references.TopLevel.UnionWith(result.EngineEvents);

        var roots = new List<CallStackNode>();

        var detached = new List<CallStackNode>();

        var known = new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance);

        void AddNode(CallStackNode? node)
        {
            if (node is null || references.Nodes.Contains(node))
            {
                return;
            }

            var top = AttachedRoot(node);

            if (!known.Add(top))
            {
                return;
            }

            if (top.Parent is { } parent)
            {
                AddNode(parent);

                detached.Add(top);
            }
            else
            {
                roots.Add(top);
            }
        }

        AddNode(result.CallStackTree?.Root);

        AddNode(content.FullTrace?.CallStack.Root);

        var added = new List<EngineEvent>();

        List<EngineEvent> pending = [.. result.EngineEvents];

        var scannedEvents = 0;

        var scannedRoots = 0;

        var scannedDetached = 0;

        var mappedScanned = content.FullTrace is null;

        while (true)
        {
            foreach (var engineEvent in pending)
            {
                AddEvent(engineEvent, references, added);
            }

            pending.Clear();

            for (; scannedEvents < added.Count; scannedEvents++)
            {
                foreach (var node in ExportPackage.ReferencesOf(added[scannedEvents]).OfType<CallStackNode>())
                {
                    AddNode(node);
                }
            }

            for (; scannedRoots < roots.Count; scannedRoots++)
            {
                Register(roots[scannedRoots], references, pending);
            }

            for (; scannedDetached < detached.Count; scannedDetached++)
            {
                Register(detached[scannedDetached], references, pending);
            }

            if (pending.Count > 0)
            {
                continue;
            }

            if (mappedScanned)
            {
                break;
            }

            mappedScanned = true;

            foreach (var node in content.FullTrace!.Timeline.MappedNodes)
            {
                AddNode(node);
            }
        }

        foreach (var logRecord in result.LogRecords.Concat(added.SelectMany(e => ExportPackage.ReferencesOf(e).OfType<LogRecord>())))
        {
            references.LogRecords.Add(logRecord);
        }

        var trees = new List<(ExportCallTree Tree, IReadOnlyList<CallStackNode> Roots)>(roots.Count + 1);

        var named = false;

        for (var index = 0; index < roots.Count; index++)
        {
            var isFullTrace = ReferenceEquals(roots[index], content.FullTrace?.CallStack.Root);

            var name = isFullTrace ? FullTraceTree : named ? $"tree-{index}" : EventsTree;

            named |= !isFullTrace;

            trees.Add((TreeOf(roots[index], name, content), [roots[index]]));
        }

        if (detached.Count > 0)
        {
            trees.Add((new ExportCallTree { Name = DetachedTree, File = $"calltree-{DetachedTree}.jsonl", IsDetached = true }, detached));
        }

        return trees;
    }

    private static void Register(CallStackNode root, ExportReferences references, List<EngineEvent> pending)
    {
        foreach (var node in PreOrder(root))
        {
            references.Nodes.Add(node);

            if (node.Frame is { } frame)
            {
                references.Frames.Add(frame);
            }

            pending.AddRange(node.Events);
        }
    }

    private static CallStackNode AttachedRoot(CallStackNode node)
    {
        while (node.Parent is { } parent && IsAttached(node, parent))
        {
            node = parent;
        }

        return node;
    }

    private static bool IsAttached(CallStackNode node, CallStackNode parent)
        => parent.Children.TryGetValue(node.Key, out var child) && ReferenceEquals(child, node);

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

    private static ExportCallTree TreeOf(CallStackNode root, string name, RunExportContent content)
    {
        var resultTree = content.Result.CallStackTree;

        var fullTraceTree = content.FullTrace?.CallStack;

        var isResultTree = ReferenceEquals(root, resultTree?.Root);

        var isFullTrace = ReferenceEquals(root, fullTraceTree?.Root);

        var tree = isFullTrace ? fullTraceTree : isResultTree ? resultTree : null;

        return new ExportCallTree
        {
            Name = name,
            File = $"calltree-{name}.jsonl",
            RootId = 0,
            IsResultTree = isResultTree,
            IsFullTrace = isFullTrace,
            ActivityFromTrace = tree?.ActivityFromTrace ?? false,
            ActivityMinUs = tree?.ActivityMinUs ?? 0,
            ActivityMaxUs = tree?.ActivityMaxUs ?? 0,
            ActivityBuckets = tree?.ActivityBuckets ?? 0,
            ActivityBusiest = tree?.ActivityBusiest ?? 0
        };
    }

    private static IEnumerable<CallStackNode> PreOrder(CallStackNode root)
    {
        var stack = new Stack<CallStackNode>();

        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            yield return node;

            foreach (var child in node.Children.Values.Reverse())
            {
                stack.Push(child);
            }
        }
    }

    private static ExportedNode NodeOf(CallStackNode node, ExportReferences references) => new()
    {
        Id = references.Nodes.IdOf(node),
        Parent = node.Parent is { } parent ? references.Nodes.IdOf(parent) : null,
        Detached = node.Parent is { } owner && !IsAttached(node, owner),
        Key = node.Key,
        Function = node.Frame is { } frame && node.Symbol is { Length: > 0 } symbol ? $"{frame.Module}!{symbol}" : null,
        Order = node.Order,
        Frame = node.Frame is { } nodeFrame ? references.Frames.IdOf(nodeFrame) : null,
        Rvas = node.Rvas.Count > 0 ? [.. node.Rvas.Order()] : null,
        Calls = node.Calls,
        CallActivity = node.CallActivity.Length > 0 ? node.CallActivity : null,
        ActivityCounts = node.ActivityCounts.Length > 0 ? node.ActivityCounts : null,
        AllocatedBytes = node.AllocatedBytes,
        Allocations = node.Allocations,
        Locators = LocatorsOf(node.Locators),
        Events = node.Events.Count > 0 ? [.. node.Events.Select(references.Events.IdOf)] : null
    };

    private static ExportedLocators? LocatorsOf(TimeTravelLocatorSet? locators)
    {
        if (locators is null)
        {
            return null;
        }

        var ordered = locators.Ordered().ToList();

        List<string> pages = [.. ordered.Where(l => l.Kind == LocatorKind.Page).Select(l => $"{l.Page.FileId}:{l.Page.PageId}")];

        List<string> rows = [.. ordered.Where(l => l.Kind == LocatorKind.Row).Select(l => $"{l.Page.FileId}:{l.Page.PageId}:{l.Slot}")];

        List<ulong[]> objects = [.. ordered.Where(l => l.Kind == LocatorKind.ColumnStoreObject).Select(l => new[] { l.Value, l.Detail })];

        return new ExportedLocators
        {
            Pages = pages.Count > 0 ? pages : null,
            Rows = rows.Count > 0 ? rows : null,
            Objects = objects.Count > 0 ? objects : null
        };
    }

    private static ExportedFrame FrameOf(CallstackFrame frame, ExportReferences references) => new()
    {
        Id = references.Frames.IdOf(frame),
        Module = frame.Module,
        Address = frame.Address,
        Pdb = frame.Pdb,
        Guid = frame.Guid,
        Age = frame.Age,
        Rva = frame.Rva,
        Instance = frame.Instance,
        Signature = frame.Signature,
        Symbol = frame.Resolved?.RawSymbol,
        SymbolModule = frame.Resolved is { } resolved && resolved.Module != frame.Module ? resolved.Module : null
    };

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

    private static ExportManifest ManifestOf(RunExportContent content,
                                              List<(ExportCallTree Tree, IReadOnlyList<CallStackNode> Roots)> trees,
                                              List<ExportPlan> plans,
                                              ExportReferences references)
    {
        var result = content.Result;

        return new ExportManifest
        {
            FormatVersion = RunExport.FormatVersion,
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
            ResultSets = [.. result.ResultSets.Select(s => new ExportResultSet(s.Rows.Count, [.. s.Columns.Select(ColumnOf)]))],
            Plans = plans,
            CallTrees = [.. trees.Select(t => t.Tree with { RootId = t.Tree.IsDetached ? -1 : references.Nodes.IdOf(t.Roots[0]) })],
            ResultLogRecords = [.. result.LogRecords.Select(references.LogRecords.IdOf)],
            HasFullTrace = content.FullTrace is not null,
            EventCount = references.Events.Count,
            LogRecordCount = references.LogRecords.Count,
            NodeCount = references.Nodes.Count
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

    private static void WriteBinary(ZipArchive zip, string name, CompressionLevel compression, Action<BinaryWriter> write)
    {
        using var stream = new BufferedStream(zip.CreateEntry(name, compression).Open(), BufferSize);

        using var writer = new BinaryWriter(stream);

        writer.Write(RunExport.BinaryMagic);

        writer.Write(RunExport.FormatVersion);

        write(writer);
    }
}
