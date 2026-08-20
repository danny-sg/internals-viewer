using InternalsViewer.Internals.Columnstore.Metadata.Enums;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;

namespace InternalsViewer.Internals.Columnstore.Metadata;

/// <summary>
/// One row set backing a columnstore index, being the columnstore itself or an internal object supporting it
/// </summary>
public sealed class ColumnstoreRowset
{
    public ColumnstoreRowsetType RowsetType { get; set; }

    /// <summary>
    /// Row set id, which is the hobt id the catalog views report
    /// </summary>
    public long HobtId { get; set; }

    public List<AllocationUnit> AllocationUnits { get; } = [];

    public AllocationUnit? DataAllocationUnit
        => AllocationUnits.FirstOrDefault(a => a.AllocationUnitType == AllocationUnitType.InRowData);

    /// <summary>
    /// Where compressed segments and dictionaries live, the columnstore holding nothing in row
    /// </summary>
    public AllocationUnit? BlobAllocationUnit
        => AllocationUnits.FirstOrDefault(a => a.AllocationUnitType == AllocationUnitType.LargeObjectData);

    public PageAddress FirstPage => DataAllocationUnit?.FirstPage ?? PageAddress.Empty;

    public PageAddress RootPage => DataAllocationUnit?.RootPage ?? PageAddress.Empty;

    public PageAddress FirstIamPage => DataAllocationUnit?.FirstIamPage ?? PageAddress.Empty;

    public bool IsAllocated => AllocationUnits.Any(a => a.FirstIamPage != PageAddress.Empty);
}
