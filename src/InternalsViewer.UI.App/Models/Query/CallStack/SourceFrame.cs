using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class SourceFrame(CallStackNode node, TracePosition position, uint thread)
{
    public CallStackNode Node { get; } = node;

    public TracePosition Position { get; } = position;

    public uint Thread { get; } = thread;

    public string Symbol => Node.Symbol;
}
