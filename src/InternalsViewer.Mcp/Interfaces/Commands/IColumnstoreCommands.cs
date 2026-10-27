using InternalsViewer.Mcp.Models.Context.Columnstore;

namespace InternalsViewer.Mcp.Interfaces.Commands;

public interface IColumnstoreCommands
{
    Task<string> OpenAsync(ColumnstoreItem item, int? rowGroup, int? column, int? dictionary);

    Task<string> ShowDocumentAsync(string name);

    Task<string> ShowMetadataTabAsync(ColumnstoreMetadataTab tab);

    Task<string> ShowRegionAsync(string region);

    Task<string> SelectMarkerAsync(string? name);

    Task<string> SelectRowAsync(int? index);
}