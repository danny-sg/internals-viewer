namespace InternalsViewer.Query.FullTrace.Memory.History.Graph;

public sealed class MemoryHistoryObjectTree
{
    private MemoryHistoryObjectTree(List<MemoryHistoryObjectTreeNode> nodes, bool isTruncated)
    {
        Nodes = nodes;

        IsTruncated = isTruncated;
    }

    public List<MemoryHistoryObjectTreeNode> Nodes { get; }

    public bool IsTruncated { get; }

    public static MemoryHistoryObjectTree Build(MemoryHistoryStore store,
                                                TracePosition from,
                                                TracePosition to,
                                                int limit,
                                                Func<int, int> getCreator)
    {
        var blocks = new List<int>();

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            var position = store.Blocks[block].FromPosition;

            if (position >= from && position <= to)
            {
                blocks.Add(block);
            }
        }

        blocks.Sort((left, right) => store.Blocks[left].FromPosition.CompareTo(store.Blocks[right].FromPosition));

        var isTruncated = blocks.Count > limit;

        var indexes = new Dictionary<int, int>();

        var nodes = new List<MemoryHistoryObjectTreeNode>();

        foreach (var block in blocks.Take(limit))
        {
            var parent = getCreator(block) is >= 0 and var creator && indexes.TryGetValue(creator, out var index) ? index : -1;

            indexes[block] = nodes.Count;

            nodes.Add(new MemoryHistoryObjectTreeNode(block, parent));
        }

        return new MemoryHistoryObjectTree(nodes, isTruncated);
    }
}
