using System.ComponentModel;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Indexes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Indexes;

[McpServerToolType]
public sealed class IndexCommandTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Index tab id from list_tabs; the active Index tab when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "index_zoom")]
    [Description("Select a page in the Index tab and zoom the diagram to it. The page must be in the index")]
    public async Task<string> Zoom(
        [Description("Page address as file:page, for example 1:1048")] string page,
        [Description("Zoom level, 1 is full size. The diagram opens fitted to the window, which is often close to 1, " +
                     "so use a higher level to zoom in. Defaults to 2")] float? zoom = null,
        [Description(TabDescription)] string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var address = Parse.PageAddress(page);

        if (zoom is <= 0)
        {
            throw new McpException("Zoom must be greater than zero. 1 is full size.");
        }

        var session = await Documents.OpenAsync<IndexContext>(tab, cancellationToken);

        RequireNode(session.Context, address);

        await session.RunAsync<IIndexCommands>(c => c.ZoomToPageAsync(address, zoom), cancellationToken);

        return $"Selected page {address} and zoomed from {session.Before.Zoom:0.##} to {session.Context.Zoom:0.##}.";
    }

    [McpServerTool(Name = "index_select_page")]
    [Description("Select a page in the Index tab and show its records in the details pane, or clear the page selection " +
                 "when the page is omitted")]
    public async Task<string> SelectPage([Description("Page address as file:page. Omit to clear")] string? page = null,
                                         [Description(TabDescription)] string? tab = null,
                                         CancellationToken cancellationToken = default)
    {
        var address = page is null ? (PageAddress?)null : Parse.PageAddress(page);

        var session = await Documents.OpenAsync<IndexContext>(tab, cancellationToken);

        if (address is { } requested)
        {
            RequireNode(session.Context, requested);
        }

        await session.RunAsync<IIndexCommands>(c => c.SelectPageAsync(address), cancellationToken);

        if (address is null)
        {
            return "Cleared the page selection.";
        }

        return $"Selected page {address} with {session.Context.Records.Count} record(s) in the details pane.";
    }

    [McpServerTool(Name = "index_select_row")]
    [Description("Select a record by slot in the details pane of the selected page, or clear the row selection when the slot " +
                 "is omitted")]
    public async Task<string> SelectRow([Description("Slot number. Omit to clear")] int? slot = null,
                                        [Description(TabDescription)] string? tab = null,
                                        CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<IndexContext>(tab, cancellationToken);

        if (slot is { } requested)
        {
            var context = session.Context;

            if (context.SelectedNode is null || context.Records.Count == 0)
            {
                throw new McpException("No page is selected. Use index_select_page first.");
            }

            if (context.Records.All(r => r.Slot != requested))
            {
                throw new McpException(
                    $"Page {context.SelectedNode.PageAddress} has no record in slot {requested}. " +
                    $"It has {context.Records.Count} record(s).");
            }
        }

        await session.RunAsync<IIndexCommands>(c => c.SelectRowAsync(slot), cancellationToken);

        var selected = session.Context;

        return selected.SelectedSlot is { } current
            ? $"Selected the record in slot {current} of page {selected.SelectedNode?.PageAddress}."
            : "Cleared the row selection.";
    }

    private static void RequireNode(IndexContext context, PageAddress address)
    {
        if (context.Nodes.All(n => n.PageAddress != address))
        {
            throw new McpException($"Page {address} is not in this index. Use index_structure to see its pages.");
        }
    }
}
