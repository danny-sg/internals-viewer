using System.ComponentModel;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Pages;
using InternalsViewer.Mcp.Models.Context.Queries;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Pages;

[McpServerToolType]
public sealed class QueryPageTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Query tab id from list_tabs; the active Query tab when omitted";

    private const string PageDescription = "Address as file:page of a page open in the Query tab's page dock; the active page " +
                                           "in the dock when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "query_page_read", ReadOnly = true)]
    [Description("Read a page open in a Query tab's page dock: its header, slot table and the records on it with their column " +
                 "values. Use query_page_goto to open another page first")]
    public async Task<string> Read([Description("Only this slot's record, with every field")] int? slot = null,
                                   [Description("Records to list")] int limit = 50,
                                   [Description("First record to list")] int offset = 0,
                                   [Description("Width of a field value")] int width = 60,
                                   [Description(PageDescription)] string? page = null,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
    {
        var session = await OpenAsync(page, tab, cancellationToken);

        return PageTools.Read(session.Context, slot, limit, offset, width);
    }

    [McpServerTool(Name = "query_page_goto")]
    [Description("Open a page in a Query tab's page dock, or switch to it when it is already open, optionally selecting a slot")]
    public async Task<string> GoTo([Description("Page address as file:page, for example 1:1048")] string page,
                                   [Description("Slot to select once loaded")] int? slot = null,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
    {
        var address = Parse.PageAddress(page);

        var selectedSlot = slot is { } s ? (ushort?)s : null;

        var query = await Documents.OpenAsync<QueryContext>(tab, cancellationToken);

        await query.RunAsync<IQueryCommands>(c => c.ShowPageAsync(address, selectedSlot), cancellationToken);

        var session = await Documents.OpenQueryPageAsync(query.Tab.Id, address, cancellationToken);

        if (session.Context.Page is null)
        {
            throw new McpException($"Page {address} could not be loaded.");
        }

        return PageCommandTools.DescribeLoaded(session.Context);
    }

    [McpServerTool(Name = "query_page_select_slot")]
    [Description("Select a slot in a page open in a Query tab's page dock. The hex viewer scrolls to it and its record fields " +
                 "become the markers. Structures such as the page header and the offset table are selected as slots with " +
                 "negative numbers that differ by page type, so take them from the structures line of query_page_read")]
    public async Task<string> SelectSlot([Description("Slot number, or the negative number of a structure")] int slot,
                                         [Description(PageDescription)] string? page = null,
                                         [Description(TabDescription)] string? tab = null,
                                         CancellationToken cancellationToken = default)
    {
        var session = await OpenAsync(page, tab, cancellationToken);

        return await PageCommandTools.SelectSlotAsync(session, slot, cancellationToken);
    }

    [McpServerTool(Name = "query_page_select_marker")]
    [Description("Select a marker (a field or structure) in the hex viewer of a page open in a Query tab's page dock, or clear " +
                 "the marker selection when the name is omitted. The names come from query_page_read")]
    public async Task<string> SelectMarker([Description("Marker name, matched case insensitively. Omit to clear")] string? name = null,
                                           [Description(PageDescription)] string? page = null,
                                           [Description(TabDescription)] string? tab = null,
                                           CancellationToken cancellationToken = default)
    {
        var session = await OpenAsync(page, tab, cancellationToken);

        return await PageCommandTools.SelectMarkerAsync(session, name, cancellationToken);
    }

    [McpServerTool(Name = "query_page_scroll")]
    [Description("Scroll the hex viewer of a page open in a Query tab's page dock to a byte offset, 0 to 8191")]
    public async Task<string> Scroll([Description("Byte offset from the start of the page")] int offset,
                                     [Description(PageDescription)] string? page = null,
                                     [Description(TabDescription)] string? tab = null,
                                     CancellationToken cancellationToken = default)
    {
        PageCommandTools.RequireOffset(offset);

        var session = await OpenAsync(page, tab, cancellationToken);

        return await PageCommandTools.ScrollAsync(session, offset, cancellationToken);
    }

    [McpServerTool(Name = "query_page_view")]
    [Description("Switch the view of a page open in a Query tab's page dock: Markers (the page header or slot being viewed), " +
                 "Allocations, Pfs or Data. Only the views the page has are available")]
    public async Task<string> View([Description("Markers, Allocations, Pfs or Data")] PageViewTab view,
                                   [Description(PageDescription)] string? page = null,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
    {
        var session = await OpenAsync(page, tab, cancellationToken);

        return await PageCommandTools.ViewAsync(session, view, cancellationToken);
    }

    [McpServerTool(Name = "query_page_open_index")]
    [Description("Open the index a page in a Query tab's page dock belongs to in a new Index tab, from its root page. Only for " +
                 "pages of an index")]
    public async Task<string> OpenIndex([Description(PageDescription)] string? page = null,
                                        [Description(TabDescription)] string? tab = null,
                                        CancellationToken cancellationToken = default)
    {
        var session = await OpenAsync(page, tab, cancellationToken);

        return await PageCommandTools.OpenIndexAsync(session, cancellationToken);
    }

    private Task<McpAreaSession<PageContext>> OpenAsync(string? page, string? tab, CancellationToken cancellationToken)
        => Documents.OpenQueryPageAsync(tab, page is null ? null : Parse.PageAddress(page), cancellationToken);
}
