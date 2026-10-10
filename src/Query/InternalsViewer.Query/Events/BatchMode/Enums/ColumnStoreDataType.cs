namespace InternalsViewer.Query.Events.BatchMode.Enums;

public enum ColumnStoreDataType
{
    Int = 0,
    DateTimeOffsetGreaterThan64Bits = 1,
    Decimal = 2,
    DecimalGreaterThan64Bits = 3,
    Float = 4,
    Double = 5,
    String = 6,
    Bytes8 = 7,
    Lob = 8
}
