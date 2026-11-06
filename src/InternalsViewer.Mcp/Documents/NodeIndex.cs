using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.Mcp.Documents;

public sealed class NodeIndex
{
    private Lock Sync { get; } = new();

    private Dictionary<CallTreeNode, int> Ids { get; } = new(ReferenceEqualityComparer.Instance);

    private List<CallTreeNode> Nodes { get; } = [];

    public int Count
    {
        get
        {
            lock (Sync)
            {
                return Nodes.Count;
            }
        }
    }

    public void AddTree(CallTreeNode root)
    {
        lock (Sync)
        {
            foreach (var node in PreOrder(root))
            {
                Register(node);
            }
        }
    }

    public int GetId(CallTreeNode node)
    {
        lock (Sync)
        {
            return Register(node);
        }
    }

    public CallTreeNode? GetNode(int id)
    {
        lock (Sync)
        {
            return id >= 0 && id < Nodes.Count ? Nodes[id] : null;
        }
    }

    public CallTreeNode[] GetNodes()
    {
        lock (Sync)
        {
            return [.. Nodes];
        }
    }

    public static IEnumerable<CallTreeNode> PreOrder(CallTreeNode root)
    {
        var stack = new Stack<CallTreeNode>();

        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            yield return node;

            foreach (var child in node.Children.Values.Reverse())
            {
                stack.Push(child);
            }
        }
    }

    private int Register(CallTreeNode node)
    {
        if (Ids.TryGetValue(node, out var id))
        {
            return id;
        }

        id = Nodes.Count;

        Ids[node] = id;

        Nodes.Add(node);

        return id;
    }
}
