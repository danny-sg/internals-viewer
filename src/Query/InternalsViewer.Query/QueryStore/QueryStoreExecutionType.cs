namespace InternalsViewer.Query.QueryStore;

public enum QueryStoreExecutionType
{
    Regular = 0,
    UnderPerformanceAffectingConditions = 1,
    Partial = 2,
    Aborted = 3,
    Exception = 4
}
