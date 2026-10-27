using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Mcp.Documents;

public sealed class NodeIndex
{
    private Lock Sync { get; } = new();

    private Dictionary<CallStackNode, int> Ids { get; } = new(ReferenceEqualityComparer.Instance);

    private List<CallStackNode> Nodes { get; } = [];

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

    public void AddTree(CallStackNode root)
    {
        lock (Sync)
        {
            foreach (var node in PreOrder(root))
            {
                Register(node);
            }
        }
    }

    public int GetId(CallStackNode node)
    {
        lock (Sync)
        {
            return Register(node);
        }
    }

    public CallStackNode? GetNode(int id)
    {
        lock (Sync)
        {
            return id >= 0 && id < Nodes.Count ? Nodes[id] : null;
        }
    }

    public CallStackNode[] GetNodes()
    {
        lock (Sync)
        {
            return [.. Nodes];
        }
    }

    public static IEnumerable<CallStackNode> PreOrder(CallStackNode root)
    {
        var stack = new Stack<CallStackNode>();

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

    private int Register(CallStackNode node)
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
