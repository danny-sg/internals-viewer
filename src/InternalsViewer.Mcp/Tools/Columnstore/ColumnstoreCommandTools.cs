using System.ComponentModel;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Columnstore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Columnstore;

[McpServerToolType]
public sealed class ColumnstoreCommandTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Columnstore tab id from list_tabs; the active Columnstore tab when omitted";

    private const string EntriesRegion = "Entries";

    private const int MaximumListed = 40;

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "columnstore_open")]
    [Description("Open a Segment, Dictionary, DeltaStore or DeleteBitmap of the columnstore index in its own tab, or select it " +
                 "if it is open already. Segment needs rowGroup and column. Dictionary needs column and, for a local " +
                 "dictionary, dictionary. DeltaStore needs rowGroup. The ids come from the columnstore tools")]
    public async Task<string> Open(
        [Description("Segment, Dictionary, DeltaStore or DeleteBitmap")] ColumnstoreItem item,
        [Description("Row group id")] int? rowGroup = null,
        [Description("Column id")] int? column = null,
        [Description("Dictionary id. Omit for the global dictionary of the column")] int? dictionary = null,
        [Description(TabDescription)] string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var missing = item switch
        {
            ColumnstoreItem.Segment when rowGroup is null || column is null => "A segment needs rowGroup and column.",
            ColumnstoreItem.Dictionary when column is null => "A dictionary needs column.",
            ColumnstoreItem.DeltaStore when rowGroup is null => "A delta store needs rowGroup.",
            _ => null
        };

        if (missing is not null)
        {
            throw new McpException(missing);
        }

        var session = await Documents.OpenAsync<ColumnstoreContext>(tab, cancellationToken);

        RequireLoaded(session.Context);

        await session.RunAsync<IColumnstoreCommands>(c => c.OpenAsync(item, rowGroup, column, dictionary), cancellationToken);

        return $"Opened {session.Context.CurrentDocument}.";
    }

    [McpServerTool(Name = "columnstore_show")]
    [Description("Bring one of the Columnstore tab's open documents forward by name: Structure, Metadata or the title of an " +
                 "opened segment, dictionary, delta store or delete bitmap. get_selection lists them")]
    public async Task<string> Show([Description("Document name, matched case insensitively")] string document,
                                   [Description(TabDescription)] string? tab = null,
                                   CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<ColumnstoreContext>(tab, cancellationToken);

        var documents = session.Context.Documents;

        if (!documents.Any(d => d.Contains(document, StringComparison.OrdinalIgnoreCase)))
        {
            throw new McpException($"No open document matches '{document}'. Open: {string.Join(", ", documents)}.");
        }

        await session.RunAsync<IColumnstoreCommands>(c => c.ShowDocumentAsync(document), cancellationToken);

        return $"Showing {session.Context.CurrentDocument}.";
    }

    [McpServerTool(Name = "columnstore_metadata_tab")]
    [Description("Switch the Metadata document between its Segments and Dictionaries lists")]
    public async Task<string> MetadataTab([Description("Segments or Dictionaries")] ColumnstoreMetadataTab list,
                                          [Description(TabDescription)] string? tab = null,
                                          CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<ColumnstoreContext>(tab, cancellationToken);

        await session.RunAsync<IColumnstoreCommands>(c => c.ShowMetadataTabAsync(list), cancellationToken);

        return $"Showing the {session.Context.MetadataTab} list.";
    }

    [McpServerTool(Name = "columnstore_region")]
    [Description("Switch the region tab of the open segment or dictionary and move its hex view there. The regions come from " +
                 "get_selection")]
    public async Task<string> Region([Description("Region name, matched case insensitively")] string region,
                                     [Description(TabDescription)] string? tab = null,
                                     CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<ColumnstoreContext>(tab, cancellationToken);

        RequireDocument(session.Context);

        var regions = session.Context.AvailableRegions;

        if (!regions.Any(r => string.Equals(r, region, StringComparison.OrdinalIgnoreCase)))
        {
            throw new McpException($"No region '{region}'. Regions: {string.Join(", ", regions)}.");
        }

        await session.RunAsync<IColumnstoreCommands>(c => c.ShowRegionAsync(region), cancellationToken);

        return $"Showing the {session.Context.Region} region.";
    }

    [McpServerTool(Name = "columnstore_select_marker")]
    [Description("Select a marker in the hex view of the open segment or dictionary by name, or clear the marker selection " +
                 "when the name is omitted. The names come from get_selection")]
    public async Task<string> SelectMarker(
        [Description("Marker name, matched case insensitively. Omit to clear")] string? name = null,
        [Description(TabDescription)] string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<ColumnstoreContext>(tab, cancellationToken);

        RequireDocument(session.Context);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var names = session.Context.MarkerNames;

            if (!names.Any(n => n.Contains(name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new McpException($"No marker matches '{name}'. Markers: {string.Join(", ", names.Take(MaximumListed))}.");
            }
        }

        await session.RunAsync<IColumnstoreCommands>(c => c.SelectMarkerAsync(name), cancellationToken);

        var context = session.Context;

        if (context.SelectedMarker is null)
        {
            return "Cleared the marker selection.";
        }

        return $"Selected {context.SelectedMarker} at offset {context.SelectedMarkerOffset}, " +
               $"{context.SelectedMarkerLength} bytes.";
    }

    [McpServerTool(Name = "columnstore_select_row")]
    [Description("Select a row of the open segment, or an entry of the open dictionary, by index and move the hex view to " +
                 "where it is stored. Clears the selection when the index is omitted")]
    public async Task<string> SelectRow(
        [Description("Row ordinal for a segment, entry index for a dictionary. Omit to clear")] int? index = null,
        [Description(TabDescription)] string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<ColumnstoreContext>(tab, cancellationToken);

        RequireDocument(session.Context);

        if (index is < 0)
        {
            throw new McpException("Row and entry indexes start at 0.");
        }

        await session.RunAsync<IColumnstoreCommands>(c => c.SelectRowAsync(index), cancellationToken);

        var context = session.Context;

        var noun = context.AvailableRegions.Contains(EntriesRegion) ? "entry" : "row";

        return context.SelectedRow is { } selected
            ? $"Selected {noun} {selected} of {context.CurrentDocument}."
            : $"Cleared the {noun} selection.";
    }

    private static void RequireLoaded(ColumnstoreContext context)
    {
        if (!context.IsLoaded)
        {
            throw new McpException("The columnstore index is still loading. Try again shortly.");
        }
    }

    private static void RequireDocument(ColumnstoreContext context)
    {
        RequireLoaded(context);

        if (context.AvailableRegions.Count == 0)
        {
            throw new McpException(
                $"The current document, {context.CurrentDocument}, is not a segment or dictionary. " +
                "Open one with columnstore_open or bring one forward with columnstore_show.");
        }
    }
}
