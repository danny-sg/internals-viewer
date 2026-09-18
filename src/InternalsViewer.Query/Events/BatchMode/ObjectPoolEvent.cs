using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events.BatchMode.Enums;
using InternalsViewer.Query.Events.Properties;

namespace InternalsViewer.Query.Events.BatchMode;

/// <summary>
/// A lookup into the columnstore object pool for a segment or dictionary
/// </summary>
public sealed partial record ObjectPoolEvent : EngineEvent
{
    [EventProperty("Hit")]
    public bool IsHit { get; set; }

    [EventProperty("Row Group")]
    public long? RowGroupId { get; set; }

    [EventProperty("Object Type")]
    public ColumnStoreObjectType ObjectType { get; set; }

    [EventProperty("Hobt Id")]
    public ulong HobtId { get; set; }

    [EventProperty("Column")]
    public int ColumnId { get; set; }

    public int PoolObjectId { get; set; }

    public override int ObjectId => PoolObjectId;

    public IReadOnlyList<PageAddress> Pages { get; set; } = [];

    public override string Name => IsHit ? "Object Pool Hit" : "Object Pool Miss";

    public override string Description
        => this switch
        {
            { ColumnId: < 0 } => $"{Name} ({ObjectTypeName})",
            { RowGroupId: null } => $"{Name} ({ObjectTypeName}, Column {ColumnId})",
            _ => $"{Name} ({ObjectTypeName}, Rowgroup: {RowGroupId}, Column {ColumnId})"
        };

    public override string Detail
        => $"{Description}";

    private string ObjectTypeName => ObjectType switch
    {
        ColumnStoreObjectType.ColumnSegment 
            => "Segment",
        ColumnStoreObjectType.PrimaryDictionary 
            => "Primary Dictionary",
        ColumnStoreObjectType.SecondaryDictionary 
            => "Secondary Dictionary",
        ColumnStoreObjectType.BulkInsertDictionary 
            => "Bulk Insert Dictionary",
        ColumnStoreObjectType.DeleteBitmap 
            => "Delete Bitmap",
        _ => "Object"
    };
}
