using InternalsViewer.Query.Plans.Joins;

namespace InternalsViewer.Query.Plans.Operators;

public enum OperatorKind
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