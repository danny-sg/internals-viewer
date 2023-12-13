using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Database;

public class AllocationUnit
{
    public int ObjectId { get; set; }

    public PageAddress FirstIamPage { get; set; }

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