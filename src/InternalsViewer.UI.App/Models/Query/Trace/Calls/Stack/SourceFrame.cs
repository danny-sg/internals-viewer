using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Stack;

public sealed class SourceFrame(CallTreeNode node, TracePosition position, uint thread)
{
    public CallTreeNode Node { get; } = node;

    public TracePosition Position { get; } = position;

    public uint Thread { get; } = thread;

    public string Symbol => Node.Symbol;
}
