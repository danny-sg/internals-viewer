using System.ComponentModel;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Columnstore;
using InternalsViewer.Mcp.Tabs;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Columnstore;

[McpServerToolType]
public sealed class ColumnstoreCommandTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Columnstore tab id from list_tabs; the active Columnstore tab when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "columnstore_open")]
    [Description("Open a Segment, Dictionary, DeltaStore or DeleteBitmap of the columnstore index in its own tab, or select it " +
                 "if it is open already. Segment needs rowGroup and column. Dictionary needs column and, for a local " +
                 "dictionary, dictionary. DeltaStore needs rowGroup. The ids come from the columnstore tools")]
    public Task<string> Open([Description("Segment, Dictionary, DeltaStore or DeleteBitmap")] ColumnstoreItem item,
                             [Description("Row group id")] int? rowGroup = null,
                             [Description("Column id")] int? column = null,
                             [Description("Dictionary id. Omit for the global dictionary of the column")] int? dictionary = null,
                             [Description(TabDescription)] string? tab = null,
                             CancellationToken cancellationToken = default)
        => Run(tab, c => c.OpenAsync(item, rowGroup, column, dictionary), cancellationToken);

    [McpServerTool(Name = "columnstore_show")]
    [Description("Bring one of the Columnstore tab's open documents forward by name: Structure, Metadata or the title of an " +
                 "opened segment, dictionary, delta store or delete bitmap. get_selection lists them")]
    public Task<string> Show([Description("Document name, matched case insensitively")] string document,
                             [Description(TabDescription)] string? tab = null,
                             CancellationToken cancellationToken = default)
        => Run(tab, c => c.ShowDocumentAsync(document), cancellationToken);

    [McpServerTool(Name = "columnstore_metadata_tab")]
    [Description("Switch the Metadata document between its Segments and Dictionaries lists")]
    public Task<string> MetadataTab([Description("Segments or Dictionaries")] ColumnstoreMetadataTab list,
                                    [Description(TabDescription)] string? tab = null,
                                    CancellationToken cancellationToken = default)
        => Run(tab, c => c.ShowMetadataTabAsync(list), cancellationToken);

    [McpServerTool(Name = "columnstore_region")]
    [Description("Switch the region tab of the open segment or dictionary and move its hex view there. The regions come from " +
                 "get_selection")]
    public Task<string> Region([Description("Region name, matched case insensitively")] string region,
                               [Description(TabDescription)] string? tab = null,
                               CancellationToken cancellationToken = default)
        => Run(tab, c => c.ShowRegionAsync(region), cancellationToken);

    [McpServerTool(Name = "columnstore_select_marker")]
    [Description("Select a marker in the hex view of the open segment or dictionary by name, or clear the marker selection " +
                 "when the name is omitted. The names come from get_selection")]
    public Task<string> SelectMarker([Description("Marker name, matched case insensitively. Omit to clear")] string? name = null,
                                     [Description(TabDescription)] string? tab = null,
                                     CancellationToken cancellationToken = default)
        => Run(tab, c => c.SelectMarkerAsync(name), cancellationToken);

    [McpServerTool(Name = "columnstore_select_row")]
    [Description("Select a row of the open segment, or an entry of the open dictionary, by index and move the hex view to " +
                 "where it is stored. Clears the selection when the index is omitted")]
    public Task<string> SelectRow([Description("Row ordinal for a segment, entry index for a dictionary. Omit to clear")] int? index = null,
                                  [Description(TabDescription)] string? tab = null,
                                  CancellationToken cancellationToken = default)
        => Run(tab, c => c.SelectRowAsync(index), cancellationToken);

    private Task<string> Run(string? tab, Func<IColumnstoreCommands, Task<string>> command, CancellationToken cancellationToken)
        => Documents.RunAsync(McpArea.Columnstore, tab, command, cancellationToken);
}