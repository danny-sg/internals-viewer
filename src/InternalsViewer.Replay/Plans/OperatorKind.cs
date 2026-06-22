namespace InternalsViewer.Replay.Plans;

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