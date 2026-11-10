using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceStartCall(CallTreeNode Node, TraceArgumentCall Call)
{
    public static TraceStartCall? Find(TraceCallStore store, TraceTimeline timeline)
    {
        if (timeline.GetFirstRootCall() is not { } root
            || timeline.GetNode(root.Span.CallNodeIndex) is not { Frame: { } frame } node
            || store.GetCallList(frame.Address, frame.InstanceAddress) is not { } calls)
        {
            return null;
        }

        var index = calls.IndexOfEnclosing(root.Span.From.Position, root.ThreadId);

        return index >= 0 ? new TraceStartCall(node, calls[index]) : null;
    }
}
