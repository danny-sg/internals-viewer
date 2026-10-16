using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.Query.Events.BatchMode;

public static class ColumnStoreObjectLabel
{
    public static string Get(ColumnStoreObjectType objectType, int columnId, int objectId) => objectType switch
    {
        ColumnStoreObjectType.ColumnSegment
            => $"Segment (Rowgroup {objectId}, Column {columnId})",
        ColumnStoreObjectType.PrimaryDictionary
            => $"Primary Dictionary (Column {columnId})",
        ColumnStoreObjectType.SecondaryDictionary
            => $"Secondary Dictionary {objectId} (Column {columnId})",
        ColumnStoreObjectType.BulkInsertDictionary
            => $"Bulk Insert Dictionary (Column {columnId})",
        ColumnStoreObjectType.DeleteBitmap
            => $"Delete Bitmap (Rowgroup {objectId})",
        _ => $"{objectType} (Column {columnId}, Object {objectId})"
    };
}
