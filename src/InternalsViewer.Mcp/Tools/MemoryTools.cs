using System.Buffers.Binary;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Timeline;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools;

[McpServerToolType]
public sealed class MemoryTools(McpDocumentResolver documents)
{
    private const string DocumentDescription = "Document id from list_documents; the active document when omitted";

    private static readonly TracePosition EndOfTrace = new(TracePosition.MaximumSequence, 0);

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "memory", ReadOnly = true)]
    [Description("Full Trace memory: allocations and frees per thread, peak memory in use, and the memory map grouped by class")]
    public async Task<string> Memory([Description("Regex over class name or allocation site to list matching memory map entries")]
                                     string? grep = null,
                                     [Description("Maximum rows to list")] int limit = 40,
                                     [Description(DocumentDescription)] string? document = null,
                                     CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var trace = scope.RequireTrace();

        var memory = trace.Memory;

        var timeline = trace.Timeline;

        var builder = new StringBuilder();

        var table = new TextTable("Thread", "Allocations", "Allocated", "Freed");

        foreach (var thread in timeline.Threads)
        {
            var allocations = memory.GetAllocations(thread.ThreadId).ToList();

            var freed = memory.FreedDuring(thread.ThreadId, timeline.Start.Position, EndOfTrace);

            if (allocations.Count == 0 && freed == 0)
            {
                continue;
            }

            table.Add(thread.ThreadId,
                      allocations.Count,
                      Format.Bytes(allocations.Aggregate(0ul, (sum, a) => sum + a.Bytes)),
                      Format.Bytes(freed));
        }

        if (table.Count == 0)
        {
            builder.AppendLine("no allocations recorded in the trace");
        }
        else
        {
            table.Write(builder);
        }

        if (memory.HasAllocations)
        {
            builder.AppendLine();

            builder.AppendLine($"in use: peak {Format.Bytes(memory.PeakInUseDuring(timeline.Start.Position, EndOfTrace))}");
        }

        if (memory.HasWorkspace)
        {
            var workspacePeak = memory.PeakWorkspaceDuring(timeline.Start.Position, EndOfTrace);

            builder.AppendLine($"workspace (grant) memory: peak {Format.Bytes(workspacePeak)}");
        }

        if (trace.MemoryMap.Count > 0)
        {
            builder.AppendLine();

            AppendMemoryMap(builder, scope, trace, grep, null, "class", 90, limit);
        }

        return builder.ToString();
    }

    [McpServerTool(Name = "memorymap", ReadOnly = true)]
    [Description("Full Trace memory map: allocations labelled by C++ class and allocation site, grouped, and the entries matching " +
                 "a regex or address")]
    public async Task<string> MemoryMap([Description("Regex over class name or allocation site to list matching entries")]
                                        string? grep = null,
                                        [Description("Hex address to list the entries of one block")] string? address = null,
                                        [Description("How to group the totals: class, site or operation")] string by = "class",
                                        [Description("Truncate long template class names to this width")] int width = 90,
                                        [Description("Maximum rows to list")] int limit = 40,
                                        [Description(DocumentDescription)] string? document = null,
                                        CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var trace = scope.RequireTrace();

        if (trace.MemoryMap.Count == 0)
        {
            return "no memory map in the trace";
        }

        var builder = new StringBuilder();

        AppendMemoryMap(builder, scope, trace, grep, address is null ? null : Parse.Hex(address), by, width, limit);

        return builder.ToString();
    }

    [McpServerTool(Name = "objectpool", ReadOnly = true)]
    [Description("Full Trace: the columnstore object pool snapshot at the end of the run")]
    public async Task<string> ObjectPool([Description("Maximum entries to list")] int limit = 100,
                                         [Description(DocumentDescription)] string? document = null,
                                         CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var trace = scope.RequireTrace();

        var entries = trace.ObjectPool.Entries;

        if (entries.Count == 0)
        {
            return "no object pool snapshot in the trace";
        }

        var builder = new StringBuilder();

        builder.AppendLine($"{entries.Count} columnstore object pool entries at the end of the run");

        var table = new TextTable("Type", "Db", "Hobt", "Column", "Object", "Size", "Pages KB", "Data");

        foreach (var entry in entries.Take(limit))
        {
            table.Add(entry.ObjectType,
                      entry.DatabaseId,
                      entry.HobtId,
                      entry.ColumnId,
                      entry.ObjectId,
                      Format.Bytes(entry.ObjectSize),
                      entry.PagesKb,
                      Format.Hex(entry.DataAddress));
        }

        table.Write(builder);

        builder.AppendIfNotEmpty(Format.More(entries.Count, limit, "raise limit"));

        return builder.ToString();
    }

    [McpServerTool(Name = "memoryhistory", ReadOnly = true)]
    [Description("Full Trace memory history: the blocks the query wrote to, and for one block every write with its writer, value " +
                 "and field offset")]
    public async Task<string> MemoryHistory([Description("Block number (#) whose writes to show")] int? block = null,
                                            [Description("Hex iterator object address: only blocks belonging to this iterator")]
                                            string? iterator = null,
                                            [Description("Hex address: only the block containing it")] string? address = null,
                                            [Description("Regex over the allocating function, for example CBpBatch::")]
                                            string? allocatedIn = null,
                                            [Description("Only blocks of this kind: iterator or inline")] string? kind = null,
                                            [Description("Block order: index or writes")] string sort = "index",
                                            [Description("With block, only writes at this offset, decimal or 0x hex")] string? field = null,
                                            [Description("With block, first write to show")] int offset = 0,
                                            [Description("With block, how many writers and fields to summarise")] int writers = 8,
                                            [Description("Truncate function names to this width")] int width = 70,
                                            [Description("Maximum blocks or writes to list")] int limit = 50,
                                            [Description(DocumentDescription)] string? document = null,
                                            CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var trace = scope.RequireTrace();

        var history = trace.MemoryHistory;

        if (history.Blocks.Count == 0)
        {
            return "no memory history in the trace";
        }

        var wantedKind = kind?.ToLowerInvariant() switch
        {
            null => (bool?)null,
            "iterator" => true,
            "inline" => false,
            _ => throw new McpException($"kind must be iterator or inline, not '{kind}'.")
        };

        var byWrites = sort.ToLowerInvariant() switch
        {
            "index" => false,
            "writes" => true,
            _ => throw new McpException($"sort must be index or writes, not '{sort}'.")
        };

        var timeline = trace.Timeline;

        var builder = new StringBuilder();

        builder.AppendLine($"memory history: {Format.Number(history.Blocks.Count)} watched blocks, " +
                           $"{Format.Number(history.WriteCount)} writes");

        builder.AppendLine("A block is memory the query allocated and wrote to. Kind iterator = an operator object, inline = carved from a " +
                           "larger");

        builder.AppendLine("block. Iterator = the operator object the block belongs to (0 when none). Writes are byte changes, each with its writer.");

        var selected = Enumerable.Range(0, history.Blocks.Count).Select(i => (Index: i, Block: history.Blocks[i]));

        if (iterator is not null)
        {
            var wanted = Parse.Hex(iterator);

            selected = selected.Where(b => b.Block.Iterator == wanted || b.Block.Address == wanted);
        }

        if (address is not null)
        {
            var wanted = Parse.Hex(address);

            selected = selected.Where(b => b.Block.Address <= wanted && wanted < b.Block.Address + (ulong)b.Block.Size);
        }

        if (allocatedIn is not null)
        {
            var pattern = Parse.Pattern(allocatedIn);

            selected = selected.Where(b => pattern.IsMatch(GetFunctionName(scope, timeline, b.Block.CallNodeIndex)));
        }

        if (wantedKind is { } isIterator)
        {
            selected = selected.Where(b => isIterator ? b.Block.IsIterator : b.Block.IsInline);
        }

        var blocks = selected.ToList();

        if (byWrites)
        {
            blocks = [.. blocks.OrderByDescending(b => b.Block.WriteCount)];
        }

        builder.AppendLine($"{blocks.Count} blocks selected");

        var table = new TextTable("#", "Address", "Size", "Kind", "Iterator", "Thread", "From", "To", "Writes", "Allocated in");

        foreach (var (index, entry) in blocks.Take(limit))
        {
            table.Add(index,
                      Format.Hex(entry.Address),
                      entry.Size,
                      entry.IsIterator ? "iterator" : entry.IsInline ? "inline" : string.Empty,
                      Format.Hex(entry.Iterator),
                      entry.Thread,
                      Format.Position(entry.FromPosition),
                      Format.Position(entry.ToPosition),
                      entry.WriteCount,
                      Format.Truncate(GetFunctionName(scope, timeline, entry.CallNodeIndex), width));
        }

        table.Write(builder);

        builder.AppendIfNotEmpty(Format.More(blocks.Count, limit, "raise limit or filter with iterator, address, allocatedIn, kind"));

        if (block is { } blockIndex)
        {
            if (blockIndex < 0 || blockIndex >= history.Blocks.Count)
            {
                throw new McpException($"No memory history block {blockIndex}; there are {history.Blocks.Count}.");
            }

            AppendBlock(builder, scope, timeline, history, blockIndex, field is null ? null : (int)Parse.Integer(field), offset, writers, limit);
        }

        return builder.ToString();
    }

    private static void AppendMemoryMap(StringBuilder builder,
                                        McpDocumentScope scope,
                                        TraceResult trace,
                                        string? grep,
                                        ulong? address,
                                        string by,
                                        int width,
                                        int limit)
    {
        var grouping = by.ToLowerInvariant() switch
        {
            "class" or "site" or "operation" => by.ToLowerInvariant(),
            _ => throw new McpException($"by must be class, site or operation, not '{by}'.")
        };

        var pattern = grep is null ? null : Parse.Pattern(grep);

        var timeline = trace.Timeline;

        var groups = new Dictionary<string, Group>();

        var operations = new Dictionary<string, int>();

        var matched = new List<MemoryMapEntry>();

        var count = 0;

        foreach (var entry in trace.MemoryMap.GetEntries())
        {
            count++;

            var operation = entry.Operation.ToString();

            operations[operation] = operations.GetValueOrDefault(operation) + 1;

            var key = grouping switch
            {
                "site" => entry.Site ?? "(no site)",
                "operation" => operation,
                _ => entry.ClassName ?? entry.Site ?? "(unlabelled)"
            };

            if (!groups.TryGetValue(key, out var group))
            {
                group = new Group();

                groups[key] = group;
            }

            group.Add(entry);

            if ((pattern is not null && pattern.IsMatch($"{entry.ClassName} {entry.Site}")) || (address is { } wanted && entry.Address == wanted))
            {
                matched.Add(entry);
            }
        }

        builder.AppendLine($"memory map: {Format.Number(count)} entries; " +
                           string.Join(", ", operations.OrderByDescending(o => o.Value).Select(o => $"{o.Key} {Format.Number(o.Value)}")));

        builder.AppendLine("An entry is one labelling of an address: Allocated at its allocation, Constructed when a constructor ran on it,");

        builder.AppendLine("Declared when a call declared its type, Visited when a method ran on it. Blocks counts distinct addresses.");

        var heading = grouping switch
        {
            "site" => "Allocation site",
            "operation" => "Operation",
            _ => "Class or site"
        };

        var table = new TextTable(heading, "Entries", "Blocks", "Bytes");

        foreach (var (name, group) in groups.OrderByDescending(g => g.Value.Bytes).Take(limit))
        {
            table.Add(Format.Truncate(name, width), group.Entries, group.Addresses.Count, Format.Bytes(group.Bytes));
        }

        table.Write(builder);

        if (matched.Count == 0)
        {
            return;
        }

        builder.AppendLine();

        builder.AppendLine($"{matched.Count} entries matching:");

        foreach (var entry in matched.OrderBy(e => e.Address).ThenBy(e => e.FromPosition).Take(limit))
        {
            builder.AppendLine($"  {Format.Hex(entry.Address)} {Format.Bytes(entry.Bytes)} {entry.Operation} " +
                               $"{Format.Truncate(entry.ClassName, width)} {entry.Site ?? string.Empty} thread {entry.Thread} " +
                               $"{Format.Position(entry.FromPosition)} to {Format.Position(entry.ToPosition)} owner {Format.Hex(entry.Owner)} " +
                               $"in {GetFunctionName(scope, timeline, entry.CallNodeIndex)}");
        }

        builder.AppendIfNotEmpty(Format.More(matched.Count, limit, "raise limit or narrow grep"));
    }

    private static void AppendBlock(StringBuilder builder,
                                    McpDocumentScope scope,
                                    TraceTimeline timeline,
                                    MemoryHistoryStore history,
                                    int block,
                                    int? field,
                                    int offset,
                                    int writers,
                                    int limit)
    {
        var entry = history.Blocks[block];

        var writes = history.GetWrites(block);

        var byWriter = new Dictionary<string, int>();

        var byField = new Dictionary<(int Offset, int Length), int>();

        foreach (var write in writes)
        {
            var writer = GetFunctionName(scope, timeline, write.CallNodeIndex);

            byWriter[writer] = byWriter.GetValueOrDefault(writer) + 1;

            byField[(write.Offset, write.Length)] = byField.GetValueOrDefault((write.Offset, write.Length)) + 1;
        }

        builder.AppendLine();

        builder.AppendLine($"block {block} {Format.Hex(entry.Address)} size {entry.Size}: {Format.Number(writes.Count)} writes by {byWriter.Count} functions");

        builder.AppendLine("writers: " + string.Join(", ", byWriter.OrderByDescending(w => w.Value).Take(writers).Select(w => $"{w.Key} x{Format.Number(w.Value)}")));

        builder.AppendLine("most written fields (offset/length): " +
                           string.Join(", ", byField.OrderByDescending(f => f.Value).Take(writers).Select(f => $"+0x{f.Key.Offset:X}/{f.Key.Length} x{Format.Number(f.Value)}")));

        builder.AppendLine();

        builder.AppendLine("writes (use offset and limit to page, field to follow one offset):");

        var shown = 0;

        for (var index = Math.Max(offset, 0); index < writes.Count; index++)
        {
            var write = writes[index];

            if (field is { } wantedOffset && write.Offset != wantedOffset)
            {
                continue;
            }

            var data = history.GetBytes(write);

            var value = GetValue(data);

            builder.AppendLine($"  {Format.Position(write.Position)} +0x{write.Offset:X} {write.Type} {Convert.ToHexStringLower(data)}" +
                               (value is { } number ? $" (= {Format.Hex(number)})" : string.Empty) +
                               $" by {GetFunctionName(scope, timeline, write.CallNodeIndex)} call {write.CallIndex}");

            shown++;

            if (shown >= limit)
            {
                builder.AppendLine($"  (stopped after {limit} writes)");

                break;
            }
        }
    }

    private static ulong? GetValue(ReadOnlySpan<byte> data) => data.Length switch
    {
        1 => data[0],
        2 => BinaryPrimitives.ReadUInt16LittleEndian(data),
        4 => BinaryPrimitives.ReadUInt32LittleEndian(data),
        8 => BinaryPrimitives.ReadUInt64LittleEndian(data),
        _ => null
    };

    private static string GetFunctionName(McpDocumentScope scope, TraceTimeline timeline, int callNodeIndex)
        => timeline.NodeOf(callNodeIndex) is { } node ? scope.Context.GetFunctionName(node) : $"raw node {callNodeIndex}";

    private sealed class Group
    {
        public int Entries { get; private set; }

        public ulong Bytes { get; private set; }

        public HashSet<ulong> Addresses { get; } = [];

        public void Add(MemoryMapEntry entry)
        {
            Entries++;

            Bytes += entry.Bytes;

            Addresses.Add(entry.Address);
        }
    }
}
