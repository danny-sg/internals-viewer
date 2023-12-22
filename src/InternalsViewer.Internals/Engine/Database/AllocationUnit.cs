using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database.Enums;

namespace InternalsViewer.Internals.Engine.Database;

/// <summary>
/// Allocation Units are the logical structure used by the Storage Engine for data
/// </summary>
/// <remarks>
/// Allocation Units are the key link between tables, indexes, data, and the underlying pages used in the Storage Engine.
/// 
/// Information is sourced from the undocumented system view sys.system_internals_allocation_units. sys.allocation_units is documented 
/// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-allocation-units-transact-sql"/>, but it
/// does not include the IAM entry points.
/// </remarks>
public record AllocationUnit
{
    public long AllocationUnitId { get; set; }

    public int ObjectId { get; set; }

    public int IndexId { get; set; }

    public long PartitionId { get; set; }

    public PageAddress FirstIamPage { get; set; }

    public PageAddress RootPage { get; set; }

    public PageAddress FirstPage { get; set; }

    public IamChain IamChain { get; set; } = new();

    public string SchemaName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string IndexName { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public byte IndexType { get; set; }

    public AllocationUnitType AllocationUnitType { get; set; }

    public long UsedPages { get; set; }

    public long TotalPages { get; set; }

    public static readonly AllocationUnit Unknown = new() { AllocationUnitId = -1 };

    public string DisplayName { get; set; } = string.Empty;
}