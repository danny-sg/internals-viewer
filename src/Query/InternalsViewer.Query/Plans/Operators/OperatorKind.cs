namespace InternalsViewer.Query.Plans.Operators;

internal enum OperatorKind
{
    DataAccess,
    HashJoin,
    NestedLoop,
    MergeJoin,
    Sort,
    Filter,
    Compute,
    Lookup,
    Spool,
    Exchange,
    Unknown
}