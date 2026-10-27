using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Mcp.Tabs;

namespace InternalsViewer.Mcp.Models.Context.Columnstore;

public sealed record ColumnstoreContext : AllocationUnitContext, IAreaContext
{
    public static McpArea Area => McpArea.Columnstore;

    public ColumnStoreIndex? Index { get; init; }

    public IReadOnlyList<string> Documents { get; init; } = [];

    public string? CurrentDocument { get; init; }

    public string? Region { get; init; }

    public IReadOnlyList<string> AvailableRegions { get; init; } = [];

    public ColumnstoreMetadataTab MetadataTab { get; init; }

    public IReadOnlyList<string> MarkerNames { get; init; } = [];

    public string? SelectedMarker { get; init; }

    public string? SelectedMarkerValue { get; init; }

    public int? SelectedRow { get; init; }
}
