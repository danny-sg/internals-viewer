using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Queries;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Queries;

[McpServerToolType]
public sealed class MemoryWatchTools(McpDocumentResolver documents)
{
    private const string DocumentDescription = "Query tab id from list_tabs; the active Query tab when omitted";

    private const string HexType = "hex";

    private static readonly Dictionary<string, (string DataType, int Length)> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tinyint"] = ("TinyInt", 1),
        ["smallint"] = ("SmallInt", 2),
        ["uint16"] = ("UInt16", 2),
        ["int"] = ("Int", 4),
        ["uint32"] = ("UInt32", 4),
        ["bigint"] = ("BigInt", 8),
        ["uint64"] = ("UInt64", 8),
        ["binary"] = ("binary", 1)
    };

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "memory_watch")]
    [Description("Watch a field of a memory history block, as the user does by pinning bytes in a memory tab. A watch shows the " +
                 "field's value at the playhead in the memory tab's Watches and under the object in the Object Graph, and applies " +
                 "to every object with the block's name. Watches added here are temporary: action add pins one, list shows every " +
                 "watch with the values of those added here at the playhead, clear removes the ones added here and leaves the " +
                 "user's own. Open show_memory or show_object_graph so the user can see it change")]
    public async Task<string> Watch([Description("add, list or clear")] string action = "list",
                                    [Description("With add, the block number from query_memoryhistory")] int? block = null,
                                    [Description("With add, the field's offset in the block, decimal or 0x hex")] string? offset = null,
                                    [Description("With add, tinyint, smallint, uint16, int, uint32, bigint, uint64, binary or hex")]
                                    string type = HexType,
                                    [Description("With add, the bytes watched; the type's size when omitted, required for hex")]
                                    int? length = null,
                                    [Description("With add, a label naming the field, for example Rows Returned")] string? label = null,
                                    [Description(DocumentDescription)] string? document = null,
                                    CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var history = scope.RequireTrace().MemoryHistory;

        switch (action.ToLowerInvariant())
        {
            case "list":
                return Describe(await scope.RunAsync<IQueryCommands, McpWatch[]>(c => c.GetWatchesAsync(), cancellationToken));

            case "clear":
                var removed = await scope.RunAsync<IQueryCommands, int>(c => c.ClearWatchesAsync(), cancellationToken);

                return removed == 0 ? "There were no watches added here to clear." : $"Removed {Format.Number(removed)} watches.";

            case "add":
                break;

            default:
                throw new McpException($"action must be add, list or clear, not '{action}'.");
        }

        if (block is not { } index || index < 0 || index >= history.Blocks.Count)
        {
            throw new McpException($"add needs a block from query_memoryhistory; there are {history.Blocks.Count}.");
        }

        if (offset is null)
        {
            throw new McpException("add needs the field's offset.");
        }

        var (dataType, size) = GetDataType(type, length);

        var start = (int)Parse.Integer(offset);

        if (start < 0 || start + size > history.Blocks[index].Size)
        {
            throw new McpException($"+0x{start:X} for {size} bytes is outside block {index}, which is {history.Blocks[index].Size} bytes.");
        }

        var watch = await scope.RunAsync<IQueryCommands, McpWatch>(c => c.AddWatchAsync(index, start, size, dataType, label),
                                                                   cancellationToken);

        return $"Watching {GetName(watch)} on {watch.Object} (block {index}), {size} bytes as {dataType ?? HexType}, " +
               $"{watch.Value ?? "not allocated"} at the playhead.";
    }

    private static (string? DataType, int Length) GetDataType(string type, int? length)
    {
        if (string.Equals(type, HexType, StringComparison.OrdinalIgnoreCase))
        {
            return length is > 0 and var bytes ? (null, bytes) : throw new McpException("A hex watch needs a length.");
        }

        if (!Types.TryGetValue(type, out var found))
        {
            throw new McpException($"type must be {string.Join(", ", Types.Keys)} or {HexType}, not '{type}'.");
        }

        return length is { } given && given != found.Length
            ? throw new McpException($"A {type} watch is {found.Length} bytes, not {given}.")
            : found;
    }

    private static string GetName(McpWatch watch) => watch.Label is { Length: > 0 } label ? label : $"+0x{watch.Offset:X}";

    private static string Describe(McpWatch[] watches)
    {
        if (watches.Length == 0)
        {
            return "No watches.";
        }

        var builder = new StringBuilder();

        builder.AppendLine($"{watches.Length} watches. Added here = temporary, cleared by action clear. Values are at the playhead " +
                           "for the block they were added on.");

        var table = new TextTable("Object", "Block", "Field", "Bytes", "Type", "Label", "Value", "Added");

        foreach (var watch in watches)
        {
            table.Add(watch.Object,
                      watch.Block?.ToString() ?? string.Empty,
                      $"+0x{watch.Offset:X}",
                      watch.Length,
                      watch.DataType ?? HexType,
                      watch.Label ?? string.Empty,
                      watch.Value ?? string.Empty,
                      watch.IsTemporary ? "here" : "by the user");
        }

        table.Write(builder);

        return builder.ToString();
    }
}
