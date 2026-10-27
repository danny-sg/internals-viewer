using System.ComponentModel;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Allocations;
using InternalsViewer.Mcp.Tabs;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Allocations;

[McpServerToolType]
public sealed class AllocationCommandTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Allocations tab id from list_tabs; the active Allocations tab when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "allocations_select_object")]
    [Description("Select an object in the allocation map by name so the rest of the map dims, or clear the selection when " +
                 "the object is omitted. A table name that matches several indexes selects its heap or clustered index, " +
                 "so name a nonclustered index in full to select it. The names come from allocations_objects")]
    public Task<string> SelectObject([Description("Object name, matched case insensitively. Omit to clear")] string? name = null,
                                     [Description("Add to the current selection instead of replacing it")] bool add = false,
                                     [Description(TabDescription)] string? tab = null,
                                     CancellationToken cancellationToken = default)
        => Run(tab, c => c.SelectObjectAsync(name, add), cancellationToken);

    [McpServerTool(Name = "allocations_scroll")]
    [Description("Scroll the allocation map to an object's first page, or to a page")]
    public Task<string> Scroll([Description("Object name, matched case insensitively")] string? name = null,
                               [Description("Page address as file:page, for example 1:1048")] string? page = null,
                               [Description(TabDescription)] string? tab = null,
                               CancellationToken cancellationToken = default)
    {
        var address = page is null ? (InternalsViewer.Internals.Engine.Address.PageAddress?)null : Parse.PageAddress(page);

        return Run(tab, c => c.ScrollToAsync(name, address), cancellationToken);
    }

    [McpServerTool(Name = "allocations_overlay")]
    [Description("Turn on an overlay of the allocation map, Gam, SGam, Pfs, BufferPool, Dcm or Bcm, or turn the overlay off " +
                 "when it is omitted. BufferPool needs a server connection")]
    public Task<string> Overlay([Description("Overlay to show. Omit to turn off")] AllocationOverlay? overlay = null,
                                [Description(TabDescription)] string? tab = null,
                                CancellationToken cancellationToken = default)
        => Run(tab, c => c.SetOverlayAsync(overlay), cancellationToken);

    [McpServerTool(Name = "allocations_search")]
    [Description("Set the search text that filters the object list, or clear it when the text is omitted")]
    public Task<string> Search([Description("Text to match in object names. Omit to clear")] string? text = null,
                               [Description(TabDescription)] string? tab = null,
                               CancellationToken cancellationToken = default)
        => Run(tab, c => c.SetSearchAsync(text), cancellationToken);

    private Task<string> Run(string? tab, Func<IAllocationsCommands, Task<string>> command, CancellationToken cancellationToken)
        => Documents.RunAsync(McpArea.Allocations, tab, command, cancellationToken);
}