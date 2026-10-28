using System.ComponentModel;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Indexes;
using InternalsViewer.Mcp.Tabs;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Indexes;

[McpServerToolType]
public sealed class IndexTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Index tab id from list_tabs; the active Index tab when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "index_structure", ReadOnly = true)]
    [Description("The B-tree of the index open in the Index tab: its allocation unit, root page and a row per level " +
                 "with the page count, page types and slot totals, and how many leaf pages are out of physical order")]
    public async Task<string> Structure([Description(TabDescription)] string? tab = null,
                                        CancellationToken cancellationToken = default)
    {
        var resolved = await Documents.ResolveAreaTabAsync(McpArea.Index, tab, cancellationToken);

        var nodes = await GetNodesAsync(resolved, cancellationToken);

        var unit = await TryGetAllocationUnitAsync(resolved, cancellationToken);

        var builder = new StringBuilder();

        builder.AppendLine($"{resolved.Title} in {resolved.DatabaseName}: {Format.Number(nodes.Count)} pages");

        if (unit is not null)
        {
            builder.AppendLine($"  {unit.DisplayName} {unit.IndexType}, allocation unit {unit.AllocationUnitId}, " +
                               $"root {unit.RootPage}, first {unit.FirstPage}");
        }

        if (nodes.Count == 0)
        {
            builder.AppendLine("  The index has not loaded any pages yet.");

            return builder.ToString();
        }

        var table = new TextTable("Height", "Depth", "Pages", "Types", "Slots", "AvgSlots", "FirstPage");

        foreach (var level in nodes.GroupBy(n => n.IndexLevel).OrderByDescending(g => g.Key))
        {
            var slots = level.Sum(n => (long)n.SlotCount);

            var types = string.Join(", ", level.GroupBy(n => n.PageType).Select(g => $"{g.Key} {g.Count()}"));

            table.Add(level.Key,
                      level.Min(n => n.Level),
                      Format.Number(level.Count()),
                      types,
                      Format.Number(slots),
                      (slots / (double)level.Count()).ToString("N1"),
                      level.OrderBy(n => n.Ordinal).First().PageAddress);
        }

        table.Write(builder);

        var leaves = nodes.Where(n => n.IndexLevel == 0 && n.NextPage.PageId != 0).ToList();

        var outOfOrder = leaves.Count(n => n.NextPage.FileId != n.PageAddress.FileId
                                           || n.NextPage.PageId != n.PageAddress.PageId + 1);

        builder.AppendLine($"Height 0 is the leaf. {outOfOrder} of {leaves.Count} leaf pages are not followed by the next " +
                           "physical page.");

        return builder.ToString();
    }

    [McpServerTool(Name = "index_nodes", ReadOnly = true)]
    [Description("The pages of the index open in the Index tab, in tree order, with their level, parent and the previous and " +
                 "next page in the chain")]
    public async Task<string> Nodes([Description("Only this height, where 0 is the leaf")] int? height = null,
                                    [Description("Only the children of this page, as file:page")] string? parent = null,
                                    [Description("Most pages to list")] int limit = 50,
                                    [Description("Pages to skip")] int offset = 0,
                                    [Description(TabDescription)] string? tab = null,
                                    CancellationToken cancellationToken = default)
    {
        var resolved = await Documents.ResolveAreaTabAsync(McpArea.Index, tab, cancellationToken);

        var nodes = await GetNodesAsync(resolved, cancellationToken);

        PageAddress? parentAddress = parent is null ? null : Parse.PageAddress(parent);

        var matching = nodes
                       .Where(n => height is null || n.IndexLevel == height)
                       .Where(n => parentAddress is null || n.Parent == parentAddress)
                       .OrderBy(n => n.Level)
                       .ThenBy(n => n.Ordinal)
                       .ToList();

        var table = new TextTable("Page", "Height", "Type", "Parent", "Prev", "Next", "Slots");

        foreach (var node in matching.Skip(offset).Take(limit))
        {
            table.Add(node.PageAddress,
                      node.IndexLevel,
                      node.PageType,
                      node.Parent,
                      node.PreviousPage,
                      node.NextPage,
                      node.SlotCount);
        }

        var builder = new StringBuilder();

        builder.AppendLine($"{matching.Count} pages");

        table.Write(builder);

        builder.AppendIfNotEmpty(Format.More(matching.Count, offset + limit, "use offset and limit, or height and parent"));

        return builder.ToString();
    }

    [McpServerTool(Name = "index_records", ReadOnly = true)]
    [Description("The records of the page selected in the Index tab, in slot order: the key values and, on a non-leaf page, " +
                 "the child page each record points to. Use index_select_page first to choose the page. Leaf pages show the " +
                 "row's columns, non-leaf pages show the lowest key of each child")]
    public async Task<string> Records([Description("First slot to list")] int fromSlot = 0,
                                      [Description("Most records to list")] int limit = 50,
                                      [Description(TabDescription)] string? tab = null,
                                      CancellationToken cancellationToken = default)
    {
        var resolved = await Documents.ResolveAreaTabAsync(McpArea.Index, tab, cancellationToken);

        var context = await resolved.GetContextAsync(cancellationToken);

        if (context is not IndexContext index)
        {
            throw new McpException($"Tab {resolved.Id} ({resolved.Area}) is not an Index tab.");
        }

        if (index.SelectedNode is null)
        {
            throw new McpException("No page is selected in the Index tab. Call index_select_page first.");
        }

        var node = index.SelectedNode;

        var builder = new StringBuilder();

        builder.AppendLine($"Page {node.PageAddress} height {node.IndexLevel} {node.PageType}, {index.Records.Count} records");

        var matching = index.Records.Where(r => r.Slot >= fromSlot).OrderBy(r => r.Slot).ToList();

        foreach (var record in matching.Take(limit))
        {
            var pointer = record.DownPage is null ? string.Empty : $" -> {record.DownPage}";

            builder.AppendLine($"  slot {record.Slot}: {string.Join(", ", record.Values)}{pointer}");
        }

        builder.AppendIfNotEmpty(Format.More(matching.Count, limit, "use fromSlot and limit"));

        return builder.ToString();
    }

    private static async Task<List<IndexNode>> GetNodesAsync(IMcpTab tab, CancellationToken cancellationToken)
    {
        var context = await tab.GetContextAsync(cancellationToken);

        return context is IndexContext index
            ? [.. index.Nodes]
            : throw new McpException($"Tab {tab.Id} ({tab.Area}) is not an Index tab.");
    }

    private static async Task<AllocationUnit?> TryGetAllocationUnitAsync(IMcpTab tab, CancellationToken cancellationToken)
    {
        return (await tab.GetContextAsync(cancellationToken)) is AllocationUnitContext context ? context.AllocationUnit : null;
    }
}
