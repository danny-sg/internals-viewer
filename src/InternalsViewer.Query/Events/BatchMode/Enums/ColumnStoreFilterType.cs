namespace InternalsViewer.Query.Events.BatchMode.Enums;

public enum ColumnStoreFilterType
{
    None = 0,
    BitmapSimpleDense = 1,
    BitmapSimple = 2,
    BitmapSimpleLarge = 3,
    BitmapComplexDense = 4,
    BitmapComplex = 5,
    BitmapComplexPrefetch = 6,
    IsNull = 7,
    IsNotNull = 8,
    CompareEqual = 9,
    CompareNotEqual = 10,
    CompareGreaterThan = 11,
    CompareLessThan = 12,
    CompareGreaterThanOrEqual = 13,
    CompareLessThanOrEqual = 14,
    BetweenGreaterThanLessThan = 15,
    BetweenGreaterThanLessThanOrEqual = 16,
    BetweenGreaterThanOrEqualLessThan = 17,
    BetweenGreaterThanOrEqualLessThanOrEqual = 18,
    Generic = 19,
    InList = 20,
    ExpressionService = 21
}
