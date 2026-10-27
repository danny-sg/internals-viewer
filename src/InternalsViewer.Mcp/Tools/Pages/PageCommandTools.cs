using System.ComponentModel;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Pages;
using InternalsViewer.Mcp.Tabs;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Pages;

[McpServerToolType]
public sealed class PageCommandTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Page tab id from list_tabs; the active Page tab when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "page_goto")]
    [Description("Load another page in the open Page tab, optionally selecting a slot. Use show_page to open a new tab instead")]
    public Task<string> GoTo([Description("Page address as file:page, for example 1:1048")] string page,
                             [Description("Slot to select once loaded")] int? slot = null,
                             [Description(TabDescription)] string? tab = null,
                             CancellationToken cancellationToken = default)
    {
        var address = Parse.PageAddress(page);

        return Run(tab, c => c.GoToPageAsync(address, slot), cancellationToken);
    }

    [McpServerTool(Name = "page_select_slot")]
    [Description("Select a slot in the Page tab. The hex viewer scrolls to it and its record fields become the markers. " +
                "Structures such as the page header and the offset table are selected as slots with negative numbers " +
                "that differ by page type, so take them from the Structures line of get_selection")]
    public Task<string> SelectSlot([Description("Slot number, or the negative number of a structure")] int slot,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
        => Run(tab, c => c.SelectSlotAsync(slot), cancellationToken);

    [McpServerTool(Name = "page_select_marker")]
    [Description("Select a marker (a field or structure) in the hex viewer by name, or clear the marker selection when the name " +
                 "is omitted. The names come from get_selection and page_read")]
    public Task<string> SelectMarker([Description("Marker name, matched case insensitively. Omit to clear")] string? name = null,
                                     [Description(TabDescription)] string? tab = null,
                                     CancellationToken cancellationToken = default)
        => Run(tab, c => c.SelectMarkerAsync(name), cancellationToken);

    [McpServerTool(Name = "page_scroll")]
    [Description("Scroll the hex viewer to a byte offset in the page, 0 to 8191")]
    public Task<string> Scroll([Description("Byte offset from the start of the page")] int offset,
                               [Description(TabDescription)] string? tab = null,
                               CancellationToken cancellationToken = default)
        => Run(tab, c => c.ScrollToAsync(offset), cancellationToken);

    [McpServerTool(Name = "page_view")]
    [Description("Switch the Page tab's view: Markers (the page header or slot being viewed), Allocations, Pfs or Data. " +
                 "Only the views the page has are available")]
    public Task<string> View([Description("Markers, Allocations, Pfs or Data")] PageViewTab view,
                             [Description(TabDescription)] string? tab = null,
                             CancellationToken cancellationToken = default)
        => Run(tab, c => c.ShowTabAsync(view), cancellationToken);

    [McpServerTool(Name = "page_open_index")]
    [Description("Open the index the page belongs to in a new Index tab, from its root page. Only for pages of an index")]
    public Task<string> OpenIndex([Description(TabDescription)] string? tab = null,
                                  CancellationToken cancellationToken = default)
        => Run(tab, c => c.OpenIndexAsync(), cancellationToken);

    private Task<string> Run(string? tab, Func<IPageCommands, Task<string>> command, CancellationToken cancellationToken)
        => Documents.RunAsync(McpArea.Page, tab, command, cancellationToken);
}