namespace InternalsViewer.Query.Events.BatchMode.Enums;

public enum ColumnStoreEncodingType
{
    None = 0,
    ValueFast = 1,
    Value = 2,
    HashPrimary32Secondary32 = 3,
    HashPrimary32Secondary64 = 4,
    HashPrimary64Secondary32 = 5,
    HashPrimary64Secondary64 = 6,
    StringValue = 7,
    StringHash = 8
}
