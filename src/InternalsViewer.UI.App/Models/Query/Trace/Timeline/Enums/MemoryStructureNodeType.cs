namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;

public enum MemoryStructureNodeType
{
    HashTable,
    BuildSide,
    ProbeSide,
    Group,
    WorktableGroup,
    Accessors,
    Partition,
    BucketPages,
    BucketPage,
    OverflowPage,
    WorkfilePages,
    WorkfilePage,
    Bitmap,
    BitmapPage,
    SortTable,
    Rowset,
    Descriptor,
    Runs,
    Run,
    RecordPages,
    RecordPage,
    ComparePages,
    ComparePage,
    Memory,
    Statement,
    Operator,
    PlanObject,
    Link
}
