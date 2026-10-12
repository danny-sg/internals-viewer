using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.Export;

public sealed class RunExportCollector
{
    internal List<(string Name, CallStackTree Tree)> Trees { get; } = [];

    internal List<IEnumerable<CallStackNode>> ReferencedNodes { get; } = [];

    public void AddTree(string name, CallStackTree tree) => Trees.Add((name, tree));

    public void AddReferencedNodes(IEnumerable<CallStackNode> nodes) => ReferencedNodes.Add(nodes);
}
