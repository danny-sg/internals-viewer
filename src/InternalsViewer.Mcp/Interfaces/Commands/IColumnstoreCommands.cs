using InternalsViewer.Mcp.Models.Context.Columnstore;

namespace InternalsViewer.Mcp.Interfaces.Commands;

public interface IColumnstoreCommands
{
    Task OpenAsync(ColumnstoreItem item, int? rowGroup, int? column, int? dictionary);

    Task ShowDocumentAsync(string name);

    Task ShowMetadataTabAsync(ColumnstoreMetadataTab tab);

    Task ShowRegionAsync(string region);

    Task SelectMarkerAsync(string? name);

    Task SelectRowAsync(int? index);
}