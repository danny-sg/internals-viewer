using System.ComponentModel;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Tabs;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Indexes;

[McpServerToolType]
public sealed class IndexCommandTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Index tab id from list_tabs; the active Index tab when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "index_zoom")]
    [Description("Select a page in the Index tab and zoom the diagram to it. The page must be in the index")]
    public Task<string> Zoom([Description("Page address as file:page, for example 1:1048")] string page,
                             [Description("Zoom level, 1 is full size. The diagram opens fitted to the window, which is often close to 1, " +
                                          "so use a higher level to zoom in. Defaults to 2")] float? zoom = null,
                             [Description(TabDescription)] string? tab = null,
                             CancellationToken cancellationToken = default)
    {
        var address = Parse.PageAddress(page);

        return Run(tab, c => c.ZoomToPageAsync(address, zoom), cancellationToken);
    }

    [McpServerTool(Name = "index_select_page")]
    [Description("Select a page in the Index tab and show its records in the details pane, or clear the page selection " +
                 "when the page is omitted")]
    public Task<string> SelectPage([Description("Page address as file:page. Omit to clear")] string? page = null,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
    {
        var address = page is null ? (Internals.Engine.Address.PageAddress?)null : Parse.PageAddress(page);

        return Run(tab, c => c.SelectPageAsync(address), cancellationToken);
    }

    [McpServerTool(Name = "index_select_row")]
    [Description("Select a record by slot in the details pane of the selected page, or clear the row selection when the slot " +
                 "is omitted")]
    public Task<string> SelectRow([Description("Slot number. Omit to clear")] int? slot = null,
                                  [Description(TabDescription)] string? tab = null,
                                  CancellationToken cancellationToken = default)
        => Run(tab, c => c.SelectRowAsync(slot), cancellationToken);

    private Task<string> Run(string? tab, Func<IIndexCommands, Task<string>> command, CancellationToken cancellationToken)
        => Documents.RunAsync(McpArea.Index, tab, command, cancellationToken);
}