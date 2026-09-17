using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Events.BatchMode;

/// <summary>A lookup into the columnstore object pool for a segment or dictionary</summary>
public sealed partial record ObjectPoolEvent : EngineEvent
{
    [EventProperty("Hit")]
    public bool IsHit { get; set; }

    [EventProperty("Object Type")]
    public ColumnStoreObjectType ObjectType { get; set; }

    [EventProperty("Hobt Id")]
    public ulong HobtId { get; set; }

    [EventProperty("Column")]
    public int ColumnId { get; set; }

    public int PoolObjectId { get; set; }

    public override int ObjectId => PoolObjectId;

    public override string Name => IsHit ? "Object Pool Hit" : "Object Pool Miss";

    public override string Description => ColumnId < 0 ? $"{Name} ({ObjectTypeName})" : $"{Name} ({ObjectTypeName}, Column {ColumnId})";

    public override string Detail
        => $"{Description} - {(IsHit ? "Served from the columnstore object pool" : "Loaded from LOB storage into the columnstore object pool")}";

    private string ObjectTypeName => ObjectType switch
    {
        ColumnStoreObjectType.ColumnSegment => "Segment",
        ColumnStoreObjectType.PrimaryDictionary => "Primary Dictionary",
        ColumnStoreObjectType.SecondaryDictionary => "Secondary Dictionary",
        ColumnStoreObjectType.BulkInsertDictionary => "Bulk Insert Dictionary",
        ColumnStoreObjectType.DeleteBitmap => "Delete Bitmap",
        _ => "Object"
    };
}
