namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

public static class PlanOutputValueLayout
{
    public const string CachedSwitchClass = "CValHashCachedSwitch";

    public const string StorageValueClass = "CValSSTable";

    public const string StorageReferenceClass = "CValRefSS";

    public const string CalculatedReferenceClass = "CValRefCalculated";

    public const string ExpressionValueClass = "CValExpr";

    public const string ExecutorClass = "CEsExec";

    public const string ExecValueClass = "CValExec";

    public const string HeapSessionClass = "HeapDataSetSession";
}
