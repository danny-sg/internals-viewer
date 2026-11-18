using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

public sealed partial class TraceNodeMap
{
    internal static TraceNodeMap Read(BinaryReader reader, IReadOnlyList<CallTreeNode> nodesById)
    {
        var parents = ExportBinary.ReadArray<int>(reader);

        CallTreeNode?[] nodes = [.. ExportBinary.ReadArray<int>(reader).Select(id => id < 0 ? null : nodesById[id])];

        return new TraceNodeMap(parents, nodes);
    }

    internal void Write(BinaryWriter writer, Func<CallTreeNode, int> getId)
    {
        ExportBinary.WriteArray(writer, Parents);

        ExportBinary.WriteArray(writer, [.. Nodes.Select(node => node is null ? -1 : getId(node))]);
    }
}
