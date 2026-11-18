using InternalsViewer.Query.FullTrace.Replay.Native;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

public sealed partial class TraceNodeMap
{
    private TraceNodeMap(int[] parents, CallTreeNode?[] nodes)
    {
        Parents = parents;
        Nodes = nodes;
    }

    public static TraceNodeMap Empty { get; } = new([], []);

    public int Count => Parents.Length;

    private int[] Parents { get; }

    private CallTreeNode?[] Nodes { get; }

    public CallTreeNode? GetNode(int replayNodeIndex)
        => replayNodeIndex >= 0 && replayNodeIndex < Nodes.Length ? Nodes[replayNodeIndex] : null;

    public int GetParent(int replayNodeIndex)
        => replayNodeIndex >= 0 && replayNodeIndex < Parents.Length ? Parents[replayNodeIndex] : -1;

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
