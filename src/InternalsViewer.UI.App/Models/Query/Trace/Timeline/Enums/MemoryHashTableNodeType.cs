namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;

public enum MemoryHashTableNodeType
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
    WorkfilePage
}
