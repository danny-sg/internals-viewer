namespace InternalsViewer.Query.FullTrace.Iterators;

internal static class IteratorSymbols
{
    public const string RowIteratorPrefix = "CQScan";

    public const string BatchIteratorPrefix = "CBpQScan";

    public const string BatchHelperClass = "CQScanBatchHelper";

    public const string QueryScanClass = "CQueryScan";

    public const string RowPlanObjectPrefix = "CXte";

    public const string BatchPlanObjectPrefix = "CBpXte";

    public const string OpenMethod = "Open";

    public const string GetRowMethod = "GetRow";

    public const string GetRowOrReQualifyMethod = "GetRowOrReQualifyHelper";

    public const string CloseMethod = "Close";

    public const string BatchOpenMethod = "BpOpen";

    public const string GetNextBatchMethod = "BpGetNextBatch";

    public const string GetBatchDataMethod = "BpGetBatchData";

    public const string BatchCloseMethod = "BpClose";

    public const string BatchFactoryMethod = "PbpqsGet";

    public const string TeardownMethod = "DestroyQuery";

    public static bool IsRowIterator(ReadOnlySpan<char> className) => className.StartsWith(RowIteratorPrefix, StringComparison.Ordinal);

    public static bool IsBatchIterator(ReadOnlySpan<char> className) => className.StartsWith(BatchIteratorPrefix, StringComparison.Ordinal);

    public static bool IsIterator(ReadOnlySpan<char> className) => IsRowIterator(className) || IsBatchIterator(className);

    public static bool IsQueryScan(ReadOnlySpan<char> className) => className.StartsWith(QueryScanClass, StringComparison.Ordinal);

    public static bool IsRowPlanObject(ReadOnlySpan<char> className) => className.StartsWith(RowPlanObjectPrefix, StringComparison.Ordinal);

    public static bool IsBatchPlanObject(ReadOnlySpan<char> className)
        => className.StartsWith(BatchPlanObjectPrefix, StringComparison.Ordinal);

    public static bool IsPlanObject(ReadOnlySpan<char> className) => IsRowPlanObject(className) || IsBatchPlanObject(className);

    public static bool IsInstanceClass(ReadOnlySpan<char> className)
        => IsIterator(className) || IsQueryScan(className) || IsPlanObject(className);

    public static bool IsIteratorMethod(ReadOnlySpan<char> method)
        => method is OpenMethod or GetRowMethod or GetRowOrReQualifyMethod or CloseMethod
                  or BatchOpenMethod or GetNextBatchMethod or GetBatchDataMethod or BatchCloseMethod;

    public static bool IsRowReturnMethod(ReadOnlySpan<char> method)
        => method is GetRowMethod or GetRowOrReQualifyMethod or GetNextBatchMethod;
}
