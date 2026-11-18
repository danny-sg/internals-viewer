namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public static class TraceHashTableLayout
{
    public const string GroupClass = "CHashWorkfilePartitionGroup";

    public const string PartitionClass = "CHashWorkfilePartitionInstance";

    public const string WorktableGroupClass = "CHashWorktablePartitionGroup";

    public const string ValueClass = "CValHash";

    public const int GroupsPerSide = 3;
}
