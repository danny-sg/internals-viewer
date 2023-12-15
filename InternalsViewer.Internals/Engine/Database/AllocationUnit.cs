using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Engine.Database;

public class AllocationUnit
{
    public long AllocationUnitId { get; set; }

    public int ObjectId { get; set; }

    public PageAddress FirstIamPage { get; set; }

    public AllocationChain IamChain { get; set; } = new();

    public string SchemaName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string IndexName { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public int IndexId { get; set; }

    public byte IndexType { get; set; }

    public AllocationUnitType AllocationUnitType { get; set; }

    public long UsedPages { get; set; }

    public long TotalPages { get; set; }
}