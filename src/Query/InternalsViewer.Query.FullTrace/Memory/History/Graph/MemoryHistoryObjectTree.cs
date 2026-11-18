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
                                                Func<int, IReadOnlyList<MemoryHistoryCallObject>> getPath)
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

        var nodes = new List<MemoryHistoryObjectTreeNode>();

        var objects = new Dictionary<(int Parent, ulong Instance), int>();

        var classes = new Dictionary<(int Parent, string ClassName), int>();

        foreach (var block in blocks.Take(limit))
        {
            var address = store.Blocks[block].Address;

            var parent = -1;

            foreach (var item in getPath(block))
            {
                if (item.Instance == address)
                {
                    break;
                }

                parent = item.Instance != 0
                    ? GetObject(nodes, objects, item, parent)
                    : GetClass(nodes, classes, item, parent);
            }

            objects[(parent, address)] = nodes.Count;

            nodes.Add(new MemoryHistoryObjectTreeNode(block, parent, Instance: address));
        }

        return new MemoryHistoryObjectTree(nodes, isTruncated);
    }

    private static int GetObject(List<MemoryHistoryObjectTreeNode> nodes,
                                 Dictionary<(int Parent, ulong Instance), int> objects,
                                 MemoryHistoryCallObject item,
                                 int parent)
    {
        if (!objects.TryGetValue((parent, item.Instance), out var index))
        {
            index = nodes.Count;

            objects[(parent, item.Instance)] = index;

            nodes.Add(new MemoryHistoryObjectTreeNode(-1, parent, item.ClassName, item.Instance));
        }

        return index;
    }

    private static int GetClass(List<MemoryHistoryObjectTreeNode> nodes,
                                Dictionary<(int Parent, string ClassName), int> classes,
                                MemoryHistoryCallObject item,
                                int parent)
    {
        if (!classes.TryGetValue((parent, item.ClassName), out var index))
        {
            index = nodes.Count;

            classes[(parent, item.ClassName)] = index;

            nodes.Add(new MemoryHistoryObjectTreeNode(-1, parent, item.ClassName));
        }

        return index;
    }
}
