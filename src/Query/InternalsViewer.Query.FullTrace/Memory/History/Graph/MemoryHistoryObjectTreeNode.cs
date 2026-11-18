namespace InternalsViewer.Query.FullTrace.Memory.History.Graph;

public readonly record struct MemoryHistoryObjectTreeNode(int Block, int Parent, string ClassName = "", ulong Instance = 0)
{
    public bool IsBlock => Block >= 0;
}
