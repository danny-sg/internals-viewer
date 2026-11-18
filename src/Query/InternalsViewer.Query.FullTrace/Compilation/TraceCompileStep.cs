using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Compilation;

public sealed class TraceCompileStep(string name, string? detail, CallTreeNode node, uint threadId, TraceSpan span)
{
    public string Name { get; } = name;

    public string? Detail { get; } = detail;

    public CallTreeNode Node { get; } = node;

    public uint ThreadId { get; } = threadId;

    public TraceSpan Span { get; } = span;

    public List<TraceCompileStep> Steps { get; } = [];

    public List<TraceCompileObject> Objects { get; } = [];

    public List<TraceExpressionTree> Trees { get; } = [];

    public bool Contains(uint threadId, TracePosition position)
        => threadId == ThreadId && Span.From.Position <= position && position < Span.To.Position;
}
