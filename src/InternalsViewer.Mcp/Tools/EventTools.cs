using System.ComponentModel;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Transactions;
using InternalsViewer.Query.Events.Waits;
using InternalsViewer.TransactionLog.LogRecords;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools;

[McpServerToolType]
public sealed class EventTools(McpDocumentResolver documents)
{
    private const string DocumentDescription = "Document id from list_documents; the active document when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "events", ReadOnly = true)]
    [Description("List or count the captured engine events, or show one event in full with its children and call stack")]
    public async Task<string> Events([Description("Show one event in full: properties, children and call stack")] int? eventId = null,
                                     [Description("Event type, for example ReadEventGroup, WaitEvent, LockEvent")] string? type = null,
                                     [Description("Event name, for example latch_acquired, lock_released")] string? name = null,
                                     [Description("Substring of the object name")] string? objectName = null,
                                     [Description("Page address file:page the event is about")] string? page = null,
                                     [Description("Thread id")] int? thread = null,
                                     [Description("Plan node id the event is attributed to")] int? node = null,
                                     [Description("Regex over type, name, description, object, pages and properties")] string? grep = null,
                                     [Description("Include folded children, not just top-level events")] bool includeChildren = false,
                                     [Description("Count by type and name instead of listing")] bool counts = false,
                                     [Description("Sort order: time or duration")] string sort = "time",
                                     [Description("Events to list")] int limit = 100,
                                     [Description("First event to list")] int offset = 0,
                                     [Description("Width of the description column")] int width = 90,
                                     [Description("Keep events overlapping from this time, in milliseconds from the query start")]
                                     double? fromMs = null,
                                     [Description("Keep events overlapping up to this time, in milliseconds")] double? toMs = null,
                                     [Description(DocumentDescription)] string? document = null,
                                     CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var builder = new StringBuilder();

        if (eventId is { } id)
        {
            var engineEvent = scope.Context.Events.GetEvent(id) ?? throw new McpException($"No event {id}.");

            WriteEvent(builder, scope, engineEvent);

            return builder.ToString();
        }

        var pageAddress = page is null ? (PageAddress?)null : Parse.PageAddress(page);

        var pattern = grep is null ? null : Parse.Pattern(grep);

        var events = GetEvents(scope, includeChildren, fromMs, toMs)
                          .Where(e => Matches(e, type, name, objectName, pageAddress, thread, node, pattern))
                          .ToList();

        if (counts)
        {
            var table = new TextTable("Type", "Name", "Count");

            foreach (var group in events.GroupBy(e => (Type: Format.EventType(e), e.Name)).OrderByDescending(g => g.Count()))
            {
                table.Add(group.Key.Type, group.Key.Name, group.Count());
            }

            table.Write(builder);

            return builder.ToString();
        }

        if (string.Equals(sort, "duration", StringComparison.OrdinalIgnoreCase))
        {
            events = [.. events.OrderByDescending(e => e.DurationUs)];
        }

        builder.AppendLine($"{events.Count} events (times are ms from the query start; includeChildren includes folded children)");

        var rows = new TextTable("Id", "Time", "Dur ms", "Thr", "Type", "Description", "Object");

        foreach (var engineEvent in events.Skip(offset).Take(limit))
        {
            rows.Add(scope.Context.Events.GetId(engineEvent),
                     Format.Relative(engineEvent.TimeUs, scope.CropStartUs),
                     Format.Milliseconds(engineEvent.DurationUs),
                     engineEvent.ThreadId,
                     Format.EventType(engineEvent),
                     Format.Truncate(Format.EventSummary(engineEvent), width),
                     Format.Truncate(engineEvent.ObjectName, 40));
        }

        rows.Write(builder);

        builder.AppendIfNotEmpty(Format.More(events.Count, offset + limit, "use offset and limit"));

        return builder.ToString();
    }

    [McpServerTool(Name = "reads", ReadOnly = true)]
    [Description("Page reads per object: disk against buffer pool, distinct pages, read-ahead, and optionally every read in time order")]
    public async Task<string> Reads([Description("Also list every read in time order")] bool list = false,
                                    [Description("Reads to list")] int limit = 200,
                                    [Description("First read to list")] int offset = 0,
                                    [Description("Width of the pages column")] int width = 80,
                                    [Description("Keep events overlapping from this time, in milliseconds from the query start")]
                                    double? fromMs = null,
                                    [Description("Keep events overlapping up to this time, in milliseconds")] double? toMs = null,
                                    [Description(DocumentDescription)] string? document = null,
                                    CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var all = GetEvents(scope, true, fromMs, toMs);

        var groups = all.OfType<ReadEventGroup>().ToList();

        var byObject = new Dictionary<string, ReadTotals>(StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var key = group.ObjectName.Length > 0 ? group.ObjectName : "(unknown object)";

            if (!byObject.TryGetValue(key, out var totals))
            {
                totals = new ReadTotals();

                byObject[key] = totals;
            }

            totals.Add(group);
        }

        var builder = new StringBuilder();

        var table = new TextTable("Object", "Disk reads", "Disk pages", "Disk distinct", "Buffer reads", "Buffer distinct", "All distinct",
                                  "Warm", "ReadAhead", "AllocPages", "Total ms");

        foreach (var (key, totals) in byObject.OrderByDescending(o => o.Value.DiskPages + o.Value.BufferPages))
        {
            table.Add(key,
                      totals.DiskReads,
                      totals.DiskPages,
                      totals.DiskDistinct.Count,
                      totals.BufferReads,
                      totals.BufferDistinct.Count,
                      totals.DiskDistinct.Union(totals.BufferDistinct).Count(),
                      totals.BufferDistinct.Except(totals.DiskDistinct).Count(),
                      totals.ReadAhead,
                      totals.AllocationPages,
                      Format.Milliseconds(totals.DurationUs));
        }

        table.Write(builder);

        builder.AppendLine();

        builder.AppendLine("A disk read covers every page in Pages (an extent is 8), a buffer pool read is one page, " +
                           "so Buffer reads is also its");

        builder.AppendLine("page count. Distinct counts each page once. Warm = buffer pool pages no disk read of this run brought in, " +
                           "so they");

        builder.AppendLine("were already in memory before the query. Buffer distinct for an operator is normally its logical reads " +
                           "in the plan.");

        var ioEvents = all.OfType<IoEvent>().Count();

        var fileEvents = all.OfType<FileEvent>().Where(e => e.Name == "file_read_completed").ToList();

        builder.AppendLine();

        builder.AppendLine($"{groups.Count} page reads, {ioEvents} physical page reads, {fileEvents.Count} file reads totalling " +
                           $"{Format.Bytes(fileEvents.Sum(e => e.Size))}");

        if (!list)
        {
            return builder.ToString();
        }

        builder.AppendLine();

        var rows = new TextTable("Id", "Time", "Dur ms", "Type", "RA", "Pages", "Object", "Node");

        foreach (var group in groups.Skip(offset).Take(limit))
        {
            rows.Add(scope.Context.Events.GetId(group),
                     Format.Relative(group.TimeUs, scope.CropStartUs),
                     Format.Milliseconds(group.DurationUs),
                     IsDisk(group) ? "disk" : "buffer",
                     group.IsReadAhead ? "RA" : string.Empty,
                     Format.Truncate(Format.CompactPages(group.Pages), width),
                     Format.Truncate(group.ObjectName, 40),
                     group.PlanNodeIdentifier?.NodeId);
        }

        rows.Write(builder);

        builder.AppendIfNotEmpty(Format.More(groups.Count, offset + limit, "use offset and limit"));

        return builder.ToString();
    }

    [McpServerTool(Name = "waits", ReadOnly = true)]
    [Description("Waits by type with counts and durations, and optionally the longest waits")]
    public async Task<string> Waits([Description("Also list the longest waits")] bool list = false,
                                    [Description("Waits to list")] int limit = 50,
                                    [Description("Keep events overlapping from this time, in milliseconds from the query start")]
                                    double? fromMs = null,
                                    [Description("Keep events overlapping up to this time, in milliseconds")] double? toMs = null,
                                    [Description(DocumentDescription)] string? document = null,
                                    CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var waits = GetEvents(scope, true, fromMs, toMs).OfType<WaitEvent>().ToList();

        var builder = new StringBuilder();

        var table = new TextTable("Wait type", "Count", "Total ms", "Max ms");

        foreach (var group in waits.GroupBy(w => w.WaitType).OrderByDescending(g => g.Sum(w => w.DurationUs)))
        {
            table.Add(group.Key,
                      group.Count(),
                      Format.Milliseconds(group.Sum(w => w.DurationUs)),
                      Format.Milliseconds(group.Max(w => w.DurationUs)));
        }

        table.Write(builder);

        if (!list)
        {
            return builder.ToString();
        }

        builder.AppendLine();

        var rows = new TextTable("Id", "Time", "Dur ms", "Thr", "Wait type", "Page", "Object");

        foreach (var wait in waits.OrderByDescending(w => w.DurationUs).Take(limit))
        {
            rows.Add(scope.Context.Events.GetId(wait),
                     Format.Relative(wait.TimeUs, scope.CropStartUs),
                     Format.Milliseconds(wait.DurationUs),
                     wait.ThreadId,
                     wait.WaitType,
                     Page(wait.PageAddress),
                     Format.Truncate(wait.ObjectName, 40));
        }

        rows.Write(builder);

        return builder.ToString();
    }

    [McpServerTool(Name = "locks", ReadOnly = true)]
    [Description("Locks by resource and mode, lock groups and escalations, and optionally every lock event in time order")]
    public async Task<string> Locks([Description("Also list every lock event in time order")] bool list = false,
                                    [Description("Lock events to list")] int limit = 200,
                                    [Description("Keep events overlapping from this time, in milliseconds from the query start")]
                                    double? fromMs = null,
                                    [Description("Keep events overlapping up to this time, in milliseconds")] double? toMs = null,
                                    [Description(DocumentDescription)] string? document = null,
                                    CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var all = GetEvents(scope, true, fromMs, toMs);

        var locks = all.OfType<LockEvent>().ToList();

        var builder = new StringBuilder();

        var table = new TextTable("Resource", "Mode", "Event", "Object", "Count");

        var grouped = locks.GroupBy(l => (l.Resource.ResourceType, l.LockMode, l.Name, l.ObjectName)).OrderByDescending(g => g.Count());

        foreach (var group in grouped)
        {
            table.Add(group.Key.ResourceType, group.Key.LockMode, group.Key.Name, Format.Truncate(group.Key.ObjectName, 50), group.Count());
        }

        table.Write(builder);

        var groups = all.OfType<LockGroup>().ToList();

        if (groups.Count > 0)
        {
            builder.AppendLine();

            builder.AppendLine("Lock groups:");

            foreach (var group in groups)
            {
                builder.AppendLine($"  [{scope.Context.Events.GetId(group)}] {Format.Relative(group.TimeUs, scope.CropStartUs)} held " +
                                   $"{Format.Milliseconds(group.DurationUs)} ms: {group.Description}");
            }
        }

        var escalations = all.OfType<LockEscalationEvent>().ToList();

        if (escalations.Count > 0)
        {
            builder.AppendLine();

            builder.AppendLine("Lock escalations:");

            foreach (var escalation in escalations)
            {
                var id = scope.Context.Events.GetId(escalation);

                builder.AppendLine($"  [{id}] {Format.Relative(escalation.TimeUs, scope.CropStartUs)} " +
                                   $"{escalation.Description}");
            }
        }

        if (!list)
        {
            return builder.ToString();
        }

        builder.AppendLine();

        var rows = new TextTable("Id", "Time", "Event", "Resource", "Mode", "Page/Key", "Xact", "Object");

        foreach (var lockEvent in locks.Take(limit))
        {
            rows.Add(scope.Context.Events.GetId(lockEvent),
                     Format.Relative(lockEvent.TimeUs, scope.CropStartUs),
                     lockEvent.Name,
                     lockEvent.Resource.ResourceType,
                     lockEvent.LockMode,
                     lockEvent.Resource.PageAddress is { } pageAddress ? Page(pageAddress) : lockEvent.Resource.KeyHash ?? string.Empty,
                     lockEvent.LockOwnerContext?.TransactionId,
                     Format.Truncate(lockEvent.ObjectName, 40));
        }

        rows.Write(builder);

        return builder.ToString();
    }

    [McpServerTool(Name = "latches", ReadOnly = true)]
    [Description("Latches by class and mode, and the most latched pages")]
    public async Task<string> Latches([Description("Pages to list")] int limit = 20,
                                      [Description("Keep events overlapping from this time, in milliseconds from the query start")]
                                      double? fromMs = null,
                                      [Description("Keep events overlapping up to this time, in milliseconds")] double? toMs = null,
                                      [Description(DocumentDescription)] string? document = null,
                                      CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var latches = GetEvents(scope, true, fromMs, toMs).OfType<LatchEvent>().ToList();

        var builder = new StringBuilder();

        var table = new TextTable("Class", "Mode", "Event", "Count");

        foreach (var group in latches.GroupBy(l => (l.LatchClass, l.LatchMode, l.Name)).OrderByDescending(g => g.Count()))
        {
            table.Add(group.Key.LatchClass, group.Key.LatchMode, group.Key.Name, group.Count());
        }

        table.Write(builder);

        var pages = latches.Where(l => l.PageAddress is not null)
                           .GroupBy(l => l.PageAddress!.Value)
                           .OrderByDescending(g => g.Count())
                           .ToList();

        builder.AppendLine();

        builder.AppendLine($"{pages.Count} distinct pages latched; most latched:");

        foreach (var group in pages.Take(limit))
        {
            builder.AppendLine($"  {Page(group.Key),-14} {group.Count()}");
        }

        return builder.ToString();
    }

    [McpServerTool(Name = "columnstore", ReadOnly = true)]
    [Description("Columnstore segment scans, rowgroups eliminated, object pool lookups and batch events")]
    public async Task<string> Columnstore([Description("Only events overlapping from this time, in milliseconds from the query " +
                                                       "start as the tools print them")]
                                          double? fromMs = null,
                                          [Description("Only events overlapping up to this time, in milliseconds from the query start")]
                                          double? toMs = null,
                                          [Description(DocumentDescription)] string? document = null,
                                          CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var all = GetEvents(scope, true, fromMs, toMs);

        var builder = new StringBuilder();

        var scans = all.OfType<SegmentScanEvent>().ToList();

        if (scans.Count > 0)
        {
            builder.AppendLine("Segment scans:");

            var table = new TextTable("Id", "Rowgroup", "Col", "Encoding", "Type", "Filter", "Compressed filter", "In", "Out",
                                      "Pure", "Impure",
                                      "Dict", "Bits");

            foreach (var scan in scans)
            {
                table.Add(scope.Context.Events.GetId(scan),
                          scan.RowGroupId,
                          scan.ColumnId,
                          scan.EncodingType,
                          scan.CompressedDataType,
                          scan.FilterType,
                          scan.IsFilterOnCompressedDataUsed ? scan.FilterOnCompressedDataType : null,
                          scan.InputRows,
                          scan.OutputRows,
                          scan.PureRowBuckets,
                          scan.ImpureRowBuckets,
                          scan.PrimaryDictionaryValueCount,
                          scan.BitPacking);
            }

            table.Write(builder);
        }

        var eliminated = all.OfType<SegmentEliminateEvent>().ToList();

        if (eliminated.Count > 0)
        {
            builder.AppendLine();

            builder.AppendLine("Rowgroups eliminated: " + string.Join(", ", eliminated.Select(Eliminated)));
        }

        var pool = all.OfType<ObjectPoolEvent>().ToList();

        if (pool.Count > 0)
        {
            var counts = pool.GroupBy(p => (p.ObjectType, Outcome: p.IsHit ? "hit" : "miss"))
                             .OrderByDescending(g => g.Count())
                             .Select(g => $"{g.Key.ObjectType} {g.Key.Outcome} x{g.Count()}");

            builder.AppendLine();

            builder.AppendLine("Object pool lookups: " + string.Join(", ", counts));
        }

        foreach (var group in all.OfType<RowGroupScanEvent>())
        {
            builder.AppendLine();

            var id = scope.Context.Events.GetId(group);

            builder.AppendLine($"[{id}] {group.Description} {Format.Relative(group.TimeUs, scope.CropStartUs)} ms " +
                               $"for {Format.Milliseconds(group.DurationUs)} ms");
        }

        var others = all.Where(e => e is ColumnStoreScanEvent or ColumnstoreFilterEvent or BatchModeEvent).ToList();

        if (others.Count > 0)
        {
            builder.AppendLine();

            foreach (var engineEvent in others)
            {
                builder.AppendLine(Format.EventLine(scope.Context, engineEvent, scope.CropStartUs));
            }
        }

        return builder.Length > 0 ? builder.ToString() : "No columnstore events were captured in this run.";
    }

    [McpServerTool(Name = "logrecords", ReadOnly = true)]
    [Description("Transaction log records captured with the run, by operation and context, and optionally each record")]
    public async Task<string> LogRecords([Description("Also list the records")] bool list = false,
                                         [Description("Records to list")] int limit = 200,
                                         [Description(DocumentDescription)] string? document = null,
                                         CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var records = GetLogRecords(scope);

        var builder = new StringBuilder();

        builder.AppendLine($"{records.Count} log records");

        var table = new TextTable("Operation", "Context", "Count");

        foreach (var group in records.GroupBy(r => (r.Operation, r.Context)).OrderByDescending(g => g.Count()))
        {
            table.Add(group.Key.Operation, group.Key.Context, group.Count());
        }

        table.Write(builder);

        if (!list)
        {
            return builder.ToString();
        }

        for (var index = 0; index < records.Count && index < limit; index++)
        {
            builder.AppendLine($"[{index}] {LogRecordLine(records[index])}");
        }

        builder.AppendIfNotEmpty(Format.More(records.Count, limit, "raise limit"));

        return builder.ToString();
    }

    private static void WriteEvent(StringBuilder builder, McpDocumentScope scope, EngineEvent engineEvent)
    {
        var events = scope.Context.Events;

        builder.AppendLine(Format.EventLine(scope.Context, engineEvent, scope.CropStartUs));

        builder.AppendLine($"  Duration: {Format.Milliseconds(engineEvent.DurationUs)} ms  Thread: {engineEvent.ThreadId}  " +
                           $"TopLevel: {events.IsTopLevel(engineEvent)}" +
                           (engineEvent.PlanNodeIdentifier is { } identifier ? $"  Plan node: {identifier.NodeId}" : string.Empty));

        foreach (var property in engineEvent.GetProperties())
        {
            builder.AppendLine($"  {property.Name}: {property.Value}");
        }

        var children = engineEvent is Query.Interfaces.Events.IEventGroup group ? group.Events : [];

        WriteChildren(builder, scope, "Events", children);

        if (engineEvent.FoldedFrom is { } folded)
        {
            WriteChildren(builder, scope, "FoldedFrom", [folded]);
        }

        if (engineEvent is ReadEventGroup { PoolLookup: { } lookup })
        {
            WriteChildren(builder, scope, "PoolLookup", [lookup]);
        }

        if (engineEvent.CallStack is { } callStack)
        {
            builder.AppendLine();

            builder.AppendLine("CallStack:");

            var path = Format.CallPath(scope.Context, callStack, 64, includeSelf: true);

            builder.AppendLine($"  node {scope.Context.Nodes.GetId(callStack)}: {path}");
        }

        if (engineEvent is TransactionLogEvent { LogRecord: { } logRecord })
        {
            builder.AppendLine();

            builder.AppendLine("LogRecord:");

            builder.AppendLine($"  {LogRecordLine(logRecord)}");
        }
    }

    private static void WriteChildren(StringBuilder builder, McpDocumentScope scope, string heading, IReadOnlyList<EngineEvent> children)
    {
        if (children.Count == 0)
        {
            return;
        }

        builder.AppendLine();

        builder.AppendLine($"{heading}:");

        foreach (var child in children)
        {
            builder.AppendLine($"  {Format.EventLine(scope.Context, child, scope.CropStartUs)}");
        }
    }

    private static List<EngineEvent> GetEvents(McpDocumentScope scope, bool includeChildren, double? fromMs, double? toMs)
    {
        var events = scope.Context.Events.GetEvents(includeChildren);

        if (fromMs is null && toMs is null)
        {
            return [.. events];
        }

        var start = fromMs is { } from ? scope.CropStartUs + (long)(from * 1000) : long.MinValue;

        var end = toMs is { } to ? scope.CropStartUs + (long)(to * 1000) : long.MaxValue;

        return [.. events.Where(e => e.TimeUs <= end && e.TimeUs + e.DurationUs >= start)];
    }

    private static bool Matches(EngineEvent engineEvent,
                                string? type,
                                string? name,
                                string? objectName,
                                PageAddress? page,
                                int? thread,
                                int? node,
                                System.Text.RegularExpressions.Regex? pattern)
    {
        if (type is not null && !string.Equals(Format.EventType(engineEvent), type, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name is not null && !string.Equals(engineEvent.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (objectName is not null && !engineEvent.ObjectName.Contains(objectName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (page is { } pageAddress && !Pages(engineEvent).Contains(pageAddress))
        {
            return false;
        }

        if (thread is { } threadId && engineEvent.ThreadId != threadId)
        {
            return false;
        }

        if (node is { } nodeId && engineEvent.PlanNodeIdentifier?.NodeId != nodeId)
        {
            return false;
        }

        return pattern is null || pattern.IsMatch(GetSearchText(engineEvent));
    }

    private static IEnumerable<PageAddress> Pages(EngineEvent engineEvent)
    {
        if (engineEvent is PageEngineEvent { PageAddress: { } pageAddress })
        {
            yield return pageAddress;
        }

        var pages = engineEvent switch
        {
            ReadEventGroup read => read.Pages,
            ObjectPoolEvent pool => pool.Pages,
            _ => []
        };

        foreach (var page in pages)
        {
            yield return page;
        }
    }

    private static string GetSearchText(EngineEvent engineEvent)
    {
        var parts = new List<string>
        {
            Format.EventType(engineEvent),
            engineEvent.Name,
            engineEvent.Description,
            engineEvent.Detail,
            engineEvent.ObjectName
        };

        parts.AddRange(Pages(engineEvent).Select(p => Page(p)));

        parts.AddRange(engineEvent.GetProperties().Select(p => $"{p.Name}: {p.Value}"));

        return string.Join("\n", parts);
    }

    private static List<LogRecord> GetLogRecords(McpDocumentScope scope)
    {
        var records = new List<LogRecord>();

        var seen = new HashSet<LogRecord>(ReferenceEqualityComparer.Instance);

        var captured = scope.Context.Events.GetEvents(includeChildren: true)
                            .OfType<TransactionLogEvent>()
                            .Select(e => e.LogRecord)
                            .OfType<LogRecord>();

        foreach (var record in scope.Result.LogRecords.Concat(captured))
        {
            if (seen.Add(record))
            {
                records.Add(record);
            }
        }

        return records;
    }

    private static string LogRecordLine(LogRecord record)
    {
        var page = record is PageLogRecord pageRecord ? $"{Page(pageRecord.PageAddress)} slot {pageRecord.SlotId} " : string.Empty;

        return $"{record.Lsn} {record.Operation}/{record.Context} {page}xact {record.LogTransactionId} {record.Description}".TrimEnd();
    }

    private static string Eliminated(SegmentEliminateEvent eliminated)
        => $"{eliminated.RowGroupId}{(eliminated.IsEliminatedByUniqueValueFilter ? " (unique value filter)" : string.Empty)}";

    private static bool IsDisk(ReadEventGroup group) => group.ReadType == ReadType.NonCached;

    private static string Page(PageAddress? pageAddress) => pageAddress is { } page ? $"{page.FileId}:{page.PageId}" : string.Empty;

    private sealed class ReadTotals
    {
        public int DiskReads { get; private set; }

        public int DiskPages { get; private set; }

        public int BufferReads { get; private set; }

        public int BufferPages { get; private set; }

        public int ReadAhead { get; private set; }

        public int AllocationPages { get; private set; }

        public long DurationUs { get; private set; }

        public HashSet<PageAddress> DiskDistinct { get; } = [];

        public HashSet<PageAddress> BufferDistinct { get; } = [];

        public void Add(ReadEventGroup group)
        {
            if (IsDisk(group))
            {
                DiskReads++;

                DiskPages += group.Pages.Count;

                DiskDistinct.UnionWith(group.Pages);
            }
            else
            {
                BufferReads++;

                BufferPages += group.Pages.Count;

                BufferDistinct.UnionWith(group.Pages);
            }

            if (group.IsReadAhead)
            {
                ReadAhead++;
            }

            if (group.IsAllocationPage)
            {
                AllocationPages++;
            }

            DurationUs += group.DurationUs;
        }
    }
}
