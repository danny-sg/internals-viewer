namespace InternalsViewer.Query.Events.BatchMode.Enums;

public enum ColumnStoreObjectType
{
    Undefined = 0,
    ColumnSegment = 1,
    PrimaryDictionary = 2,
    SecondaryDictionary = 4,
    BulkInsertDictionary = 5,
    DeleteBitmap = 6
}
