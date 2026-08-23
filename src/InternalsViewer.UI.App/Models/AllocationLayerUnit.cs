using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.UI.App.Models;

public sealed class AllocationLayerUnit
{
    public long AllocationUnitId { get; set; }

    public int? PartitionNumber { get; set; }

    public AllocationUnitType AllocationUnitType { get; set; }

    public string AllocationUnitTypeDescription => AllocationUnitType.ToString().SplitCamelCase();

    public string ColumnstoreUsage { get; set; } = string.Empty;

    public string IndexName { get; set; } = string.Empty;

    public IndexType IndexType { get; set; }

    public string IndexTypeDescription => IndexType.ToString().SplitCamelCase();

    public PageAddress FirstPage { get; set; }

    public PageAddress RootPage { get; set; }

    public PageAddress FirstIamPage { get; set; }

    public long UsedPages { get; set; }

    public long TotalPages { get; set; }

    public bool IsIndex => IndexType is IndexType.Clustered or IndexType.NonClustered && TotalPages > 0;

    public bool IsColumnstore => IndexType is IndexType.ClusteredColumnStore or IndexType.NonClusteredColumnStore && TotalPages > 0;
}
