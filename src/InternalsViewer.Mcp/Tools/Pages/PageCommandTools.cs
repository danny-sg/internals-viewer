using System.ComponentModel;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Pages;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Pages;

[McpServerToolType]
public sealed class PageCommandTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Page tab id from list_tabs; the active Page tab when omitted";

    private const int MaximumListed = 40;

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "page_goto")]
    [Description("Load another page in the open Page tab, optionally selecting a slot. Use show_page to open a new tab instead")]
    public async Task<string> GoTo([Description("Page address as file:page, for example 1:1048")] string page,
                                   [Description("Slot to select once loaded")] int? slot = null,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
    {
        var address = Parse.PageAddress(page);

        var session = await Documents.OpenAsync<PageContext>(tab, cancellationToken);

        await session.RunAsync<IPageCommands>(c => c.GoToPageAsync(address, slot), cancellationToken);

        var context = session.Context;

        var type = context.Page is { } loaded ? $" ({loaded.PageHeader.PageTypeName})" : string.Empty;

        var selected = context.CurrentSlot is { } current ? $" Selected slot {current}." : string.Empty;

        return $"Loaded page {context.Address}{type}.{selected}";
    }

    [McpServerTool(Name = "page_select_slot")]
    [Description("Select a slot in the Page tab. The hex viewer scrolls to it and its record fields become the markers. " +
                "Structures such as the page header and the offset table are selected as slots with negative numbers " +
                "that differ by page type, so take them from the Structures line of get_selection")]
    public async Task<string> SelectSlot([Description("Slot number, or the negative number of a structure")] int slot,
                                         [Description(TabDescription)] string? tab = null,
                                         CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<PageContext>(tab, cancellationToken);

        var before = session.Context;

        if (before.Page is not { } page)
        {
            throw new McpException("No page is loaded in this tab. Use page_goto to load one.");
        }

        var structure = before.Structures.FirstOrDefault(s => s.Slot == slot);

        if (structure is null && (slot < 0 || slot >= page.PageHeader.SlotCount))
        {
            var structures = string.Join(", ", before.Structures.Select(s => $"{s.Slot} ({s.Name})"));

            throw new McpException(
                $"Page {before.Address} has no slot {slot}. Slots run from 0 to {page.PageHeader.SlotCount - 1}. " +
                $"Structures: {structures}.");
        }

        await session.RunAsync<IPageCommands>(c => c.SelectSlotAsync(slot), cancellationToken);

        var description = structure is null ? string.Empty : $" ({structure.Name})";

        return $"Selected slot {session.Context.CurrentSlot}{description}.";
    }

    [McpServerTool(Name = "page_select_marker")]
    [Description("Select a marker (a field or structure) in the hex viewer by name, or clear the marker selection when the name " +
                 "is omitted. The names come from get_selection and page_read")]
    public async Task<string> SelectMarker([Description("Marker name, matched case insensitively. Omit to clear")] string? name = null,
                                           [Description(TabDescription)] string? tab = null,
                                           CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<PageContext>(tab, cancellationToken);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var names = session.Context.MarkerNames;

            if (!names.Any(n => n.Contains(name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new McpException($"No marker matches '{name}'. Markers: {string.Join(", ", names.Take(MaximumListed))}.");
            }
        }

        await session.RunAsync<IPageCommands>(c => c.SelectMarkerAsync(name), cancellationToken);

        var context = session.Context;

        if (context.SelectedName is null)
        {
            return "Cleared the marker selection.";
        }

        return $"Selected {context.SelectedName} at offset {context.SelectionStart}, {context.Selection?.Length ?? 0} bytes.";
    }

    [McpServerTool(Name = "page_scroll")]
    [Description("Scroll the hex viewer to a byte offset in the page, 0 to 8191")]
    public async Task<string> Scroll([Description("Byte offset from the start of the page")] int offset,
                                     [Description(TabDescription)] string? tab = null,
                                     CancellationToken cancellationToken = default)
    {
        if (offset < 0 || offset >= PageData.Size)
        {
            throw new McpException($"Offset {offset} is outside the page. Use 0 to {PageData.Size - 1}.");
        }

        var session = await Documents.OpenAsync<PageContext>(tab, cancellationToken);

        await session.RunAsync<IPageCommands>(c => c.ScrollToAsync(offset), cancellationToken);

        return $"Scrolled the hex viewer to offset {offset}.";
    }

    [McpServerTool(Name = "page_view")]
    [Description("Switch the Page tab's view: Markers (the page header or slot being viewed), Allocations, Pfs or Data. " +
                 "Only the views the page has are available")]
    public async Task<string> View([Description("Markers, Allocations, Pfs or Data")] PageViewTab view,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<PageContext>(tab, cancellationToken);

        if (!session.Context.AvailableTabs.Contains(view))
        {
            throw new McpException(
                $"This page has no {view} view. Available: {string.Join(", ", session.Context.AvailableTabs)}.");
        }

        await session.RunAsync<IPageCommands>(c => c.ShowTabAsync(view), cancellationToken);

        return $"Showing the {session.Context.Tab} view.";
    }

    [McpServerTool(Name = "page_open_index")]
    [Description("Open the index the page belongs to in a new Index tab, from its root page. Only for pages of an index")]
    public async Task<string> OpenIndex([Description(TabDescription)] string? tab = null,
                                        CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<PageContext>(tab, cancellationToken);

        if (session.Context.AllocationUnit is not { } unit || unit.IndexType == IndexType.Heap)
        {
            throw new McpException($"Page {session.Context.Address} does not belong to an index.");
        }

        await session.RunAsync<IPageCommands>(c => c.OpenIndexAsync(), cancellationToken);

        return "Opened the index in a new tab.";
    }
}
