using InternalsViewer.Query.FullTrace.Replay.Native;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

public sealed partial class TraceNodeMap
{
    private TraceNodeMap(int[] parents, CallTreeNode?[] nodes, bool[]? cut = null)
    {
        Parents = parents;
        Nodes = nodes;
        Cut = cut ?? [];
    }

    public static TraceNodeMap Empty { get; } = new([], []);

    public int Count => Parents.Length;

    private int[] Parents { get; }

    private CallTreeNode?[] Nodes { get; }

    private bool[] Cut { get; }

    public CallTreeNode? GetNode(int replayNodeIndex)
        => replayNodeIndex >= 0 && replayNodeIndex < Nodes.Length ? Nodes[replayNodeIndex] : null;

    public int GetParent(int replayNodeIndex)
        => replayNodeIndex >= 0 && replayNodeIndex < Parents.Length ? Parents[replayNodeIndex] : -1;

    internal bool IsCut(int replayNodeIndex) => replayNodeIndex >= 0 && replayNodeIndex < Cut.Length && Cut[replayNodeIndex];

    internal TraceNodeMap Without(IReadOnlySet<CallTreeNode> removed)
    {
        if (removed.Count == 0)
        {
            return this;
        }

        var nodes = new CallTreeNode?[Nodes.Length];

        var cut = new bool[Nodes.Length];

        for (var index = 0; index < Nodes.Length; index++)
        {
            cut[index] = Nodes[index] is { } node && removed.Contains(node);

            nodes[index] = cut[index] ? null : Nodes[index];
        }

        return new TraceNodeMap(Parents, nodes, cut);
    }

    internal static TraceNodeMap Build(IReadOnlyList<CallNode> replayed, CallTreeNode?[] nodes)
    {
        var parents = new int[replayed.Count];

        for (var index = 0; index < replayed.Count; index++)
        {
            var parent = replayed[index].ParentIndex;

            parents[index] = parent >= 0 && parent < index ? parent : -1;
        }

        return new TraceNodeMap(parents, nodes);
    }
}
