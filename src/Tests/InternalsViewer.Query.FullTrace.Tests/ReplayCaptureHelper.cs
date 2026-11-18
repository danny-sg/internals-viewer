using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Replay.Native;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Tests;

internal static class ReplayCaptureHelper
{
    public static TraceCallStore Build(this ReplayCallLog log, CallTreeNode?[]? nodes = null)
        => TraceCallStore.Build(log, TraceNodeMap.Build([.. (nodes ?? []).Select(_ => new CallNode(-1, 0, 0, 0))], nodes ?? []));

    public static TraceTimeline Build(this ReplaySpans spans, CallNode[] replayed, CallTreeNode?[]? nodes = null)
        => TraceTimeline.Build(spans, TraceNodeMap.Build(replayed, nodes ?? []));

    public static TraceBufferPool Build(this ReplayBufferPool pool) => TraceBufferPool.Build(pool);
}
