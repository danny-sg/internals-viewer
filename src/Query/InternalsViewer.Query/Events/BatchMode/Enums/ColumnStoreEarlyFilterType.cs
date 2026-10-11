namespace InternalsViewer.Query.Events.BatchMode.Enums;

public enum ColumnStoreEarlyFilterType
{
    None = 0,
    AlwaysFalse = 1,
    AlwaysTrue = 2,
    Equal = 3,
    NotEqual = 4,
    LessThan = 5,
    GreaterThan = 6,
    NotEqualNotEqual = 7,
    NotEqualLessThan = 8,
    NotEqualGreaterThan = 9,
    LessThanGreaterThan = 10,
    NotEqualLessThanGreaterThan = 11,
    RawBitmap = 12
}
