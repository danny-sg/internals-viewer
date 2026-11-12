using InternalsViewer.Query.FullTrace.Memory.Address;
using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Graph;
using InternalsViewer.Query.FullTrace.Timeline;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Queries;

[McpServerToolType]
public sealed class ObjectGraphTools(McpDocumentResolver documents)
{
    private const string DocumentDescription = "Query tab id from list_tabs; the active Query tab when omitted";

    private const int MaximumDepth = 6;

    private const int GraphLimit = 400;

    private const string EmptyObject = "(empty)";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "query_object_graph")]
    [Description("The Object Graph as text: the objects reachable through pointers from a memory history block, or from the " +
                 "objects the query's first call was given, each with the field in the object above that points to it, the object " +
                 "it holds at a position, how often that pointer moved and how often the object's other fields changed. " +
                 "show_object_graph opens the same graph in the app")]
    public async Task<string> ObjectGraph([Description("Block number from query_memoryhistory; the query's first call when omitted")]
                                          int? block = null,
                                          [Description("Levels of pointers to follow, 1 to 6")] int depth = 2,
                                          [Description("Position as hex Sequence:Steps to read the objects at; the block's start, or " +
                                                       "the first call, when omitted")]
                                          string? position = null,
                                          [Description("Maximum objects to list")] int limit = 60,
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

        var timeline = trace.Timeline;

        var from = timeline.Start.Position;

        var to = timeline.End.Position;

        var shutdown = timeline.GetFirstCallStart(TraceTimeline.ShutdownSymbol) ?? TracePosition.Unknown;

        var levels = Math.Clamp(depth, 1, MaximumDepth);

        MemoryHistoryGraph graph;

        TracePosition at;

        string root;

        if (block is { } index)
        {
            if (index < 0 || index >= history.Blocks.Count)
            {
                throw new McpException($"No memory history block {index}; there are {history.Blocks.Count}.");
            }

            at = position is null ? history.Blocks[index].FromPosition : Parse.Position(position);

            graph = MemoryHistoryGraph.Build(history,
                                             trace.BufferPool,
                                             index,
                                             from,
                                             to,
                                             shutdown,
                                             _ => true,
                                             _ => false,
                                             levels,
                                             GraphLimit);

            root = $"block {index}";
        }
        else
        {
            var start = TraceStartCall.Find(trace.CallStore, timeline)
                        ?? throw new McpException("The trace has no first call to start the Object Graph from.");

            at = position is null ? start.Call.FromPosition : Parse.Position(position);

            graph = MemoryHistoryGraph.BuildFromCall(history,
                                                     trace.BufferPool,
                                                     start.Call.IntegerSlots,
                                                     start.Call.FromPosition,
                                                     from,
                                                     to,
                                                     shutdown,
                                                     _ => true,
                                                     _ => false,
                                                     levels,
                                                     GraphLimit);

            root = $"the objects {start.Node.Symbol} was given";
        }

        var builder = new StringBuilder();

        builder.AppendLine($"object graph from {root} at {Format.Position(at)}: {Format.Number(graph.Nodes.Count)} objects, " +
                           $"{levels} levels" + (graph.IsTruncated ? ", truncated at the graph's limit" : string.Empty));

        builder.AppendLine("Field = the pointer in the object above, -> where in this object it points. Moves = how often that " +
                           "pointer moved. Changes = changes to the object's other fields.");

        var table = new TextTable("#", "Depth", "Above", "Field", "Object", "Address", "Block", "Moves", "Changes");

        for (var node = 0; node < Math.Min(graph.Nodes.Count, limit); node++)
        {
            var item = graph.Nodes[node];

            var edge = graph.Edges.FirstOrDefault(e => e.To == node && e.From == item.Parent);

            var target = GetTarget(item, at);

            table.Add(node,
                      item.Depth,
                      item.Parent >= 0 ? item.Parent.ToString() : string.Empty,
                      item.Parent >= 0 ? GetField(edge) : string.Empty,
                      new string(' ', item.Depth * 2) + GetName(trace, item, target),
                      target is { Value: not 0 } found ? Format.Hex(found.Value) : string.Empty,
                      target is { Block: >= 0 } held ? held.Block.ToString() : string.Empty,
                      item.Parent >= 0 ? edge.ChangeCount : string.Empty,
                      item.DataChangeCount);
        }

        table.Write(builder);

        builder.AppendIfNotEmpty(Format.More(graph.Nodes.Count, limit, "raise limit or depth, or start from a block"));

        return builder.ToString();
    }

    private static MemoryHistoryGraphTarget? GetTarget(MemoryHistoryGraphNode node, TracePosition position)
    {
        MemoryHistoryGraphTarget? before = null;

        foreach (var target in node.Targets)
        {
            if (target.From <= position && position < target.To)
            {
                return target;
            }

            if (target.From <= position)
            {
                before = target;
            }
        }

        return before ?? (node.Targets.Length > 0 ? node.Targets[0] : null);
    }

    private static string GetField(MemoryHistoryGraphEdge edge)
        => edge.TargetOffset is { } inside and not 0 ? $"+0x{edge.Offset:X} -> +0x{inside:X}" : $"+0x{edge.Offset:X}";

    private static string GetName(TraceResult trace, MemoryHistoryGraphNode node, MemoryHistoryGraphTarget? target)
    {
        if (target is not { } found)
        {
            return EmptyObject;
        }

        if (found.Page is { } page)
        {
            return page.IsBuffer ? $"BUF ({page.Page})" : $"Page ({page.Page})";
        }

        if (found.Block < 0)
        {
            return EmptyObject;
        }

        var history = trace.MemoryHistory;

        var block = history.Blocks[found.Block];

        var name = trace.AddressMap.GetVtableClassName(block.Address, block.FromPosition, block.ToPosition)
                   ?? trace.AddressMap.GetLabel(block.Address, block.FromPosition, block.ToPosition)
                   ?? history.GetAllocatingCall(found.Block, trace.Timeline)?.Symbol
                   ?? "Object";

        if (node.Element is { } element)
        {
            name = $"{name}[{element.Index}]";
        }

        return block.IsStack ? $"{name} (stack)" : name;
    }
}
