using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Replay;

namespace InternalsViewer.Query.FullTrace;

public sealed class TraceStore
{
    internal TraceStore(TraceCallStore calls, TraceTimeline timeline)
    {
        Calls = calls;
        Timeline = timeline;
    }

    public TraceCallStore Calls { get; }

    public TraceTimeline Timeline { get; }

    public TraceNodeMap Nodes => Timeline.Nodes;

    public TraceCallList? GetCallList(CallTreeNode node)
        => node.Frame is { } frame ? Calls.GetCallList(frame.Address, frame.InstanceAddress) : null;

    public TraceArgumentCall? GetCall(CallTreeNode node, int callIndex)
        => GetCallList(node) is { } calls && callIndex >= 0 && callIndex < calls.Count ? calls[callIndex] : null;

    public TraceArgumentCall? GetCall(TraceSpan span)
        => Nodes.GetNode(span.ReplayNodeIndex) is { } node ? GetCall(node, span.CallIndex) : null;

    public TraceSpanLocation? FindSpan(CallTreeNode node, int callIndex, TraceTimeline? view = null)
        => GetCallList(node) is { } calls ? (view ?? Timeline).FindSpan(calls, callIndex) : null;

    internal static TraceStore Build(ReplayCapture capture, TraceNodeMap nodes)
    {
        var calls = TraceCallStore.Build(capture.CallLog, nodes);

        calls.AddNodeLocators();

        return new TraceStore(calls, TraceTimeline.Build(capture.Spans, nodes));
    }
}
