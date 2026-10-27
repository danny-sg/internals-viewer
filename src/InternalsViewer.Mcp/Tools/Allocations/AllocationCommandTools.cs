using System.ComponentModel;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Allocations;
using ModelContextProtocol;
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
    public async Task<string> SelectObject(
        [Description("Object name, matched case insensitively. Omit to clear")] string? name = null,
        [Description("Add to the current selection instead of replacing it")] bool add = false,
        [Description(TabDescription)] string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<AllocationsContext>(tab, cancellationToken);

        await session.RunAsync<IAllocationsCommands>(c => c.SelectObjectAsync(name, add), cancellationToken);

        var selected = session.Context.SelectedObjects;

        return selected.Count == 0
            ? "Cleared the object selection."
            : $"Selected: {string.Join(", ", selected)}.";
    }

    [McpServerTool(Name = "allocations_scroll")]
    [Description("Scroll the allocation map to an object's first page, or to a page")]
    public async Task<string> Scroll([Description("Object name, matched case insensitively")] string? name = null,
                                     [Description("Page address as file:page, for example 1:1048")] string? page = null,
                                     [Description(TabDescription)] string? tab = null,
                                     CancellationToken cancellationToken = default)
    {
        var address = page is null ? (PageAddress?)null : Parse.PageAddress(page);

        if (address is null && string.IsNullOrWhiteSpace(name))
        {
            throw new McpException("Give an object name or a page to scroll to.");
        }

        var session = await Documents.OpenAsync<AllocationsContext>(tab, cancellationToken);

        if (address is { } requested && !session.Context.Files.Any(f => f.FileId == requested.FileId))
        {
            var files = string.Join(", ", session.Context.Files.Select(f => f.FileId));

            throw new McpException($"File {requested.FileId} is not in this database. Files: {files}.");
        }

        var target = await session.RunAsync<IAllocationsCommands, PageAddress>(
            c => c.ScrollToAsync(name, address),
            cancellationToken);

        var subject = address is null ? name : $"page {target}";

        return $"Scrolled the map to {subject} at {target} in file {session.Context.FileId}.";
    }

    [McpServerTool(Name = "allocations_overlay")]
    [Description("Turn on an overlay of the allocation map, Gam, SGam, Pfs, BufferPool, Dcm or Bcm, or turn the overlay off " +
                 "when it is omitted. BufferPool needs a server connection")]
    public async Task<string> Overlay([Description("Overlay to show. Omit to turn off")] AllocationOverlay? overlay = null,
                                      [Description(TabDescription)] string? tab = null,
                                      CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<AllocationsContext>(tab, cancellationToken);

        if (overlay == AllocationOverlay.BufferPool && session.Context.Database?.Connection is not ServerConnectionType)
        {
            throw new McpException(
                "The buffer pool overlay needs a server connection, and this database is open from a file. Use another overlay.");
        }

        await session.RunAsync<IAllocationsCommands>(c => c.SetOverlayAsync(overlay), cancellationToken);

        return session.Context.Overlay is { } shown ? $"Showing the {shown} overlay." : "Turned the overlay off.";
    }

    [McpServerTool(Name = "allocations_search")]
    [Description("Set the search text that filters the object list, or clear it when the text is omitted")]
    public async Task<string> Search([Description("Text to match in object names. Omit to clear")] string? text = null,
                                     [Description(TabDescription)] string? tab = null,
                                     CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<AllocationsContext>(tab, cancellationToken);

        await session.RunAsync<IAllocationsCommands>(c => c.SetSearchAsync(text), cancellationToken);

        var filter = session.Context.Filter;

        return filter.Length == 0 ? "Cleared the search text." : $"Searching for '{filter}'.";
    }
}
