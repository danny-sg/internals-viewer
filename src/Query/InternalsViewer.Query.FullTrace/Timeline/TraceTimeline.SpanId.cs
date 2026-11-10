namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed partial class TraceTimeline
{
    private TraceSpanIdIndex? SpanIds { get; set; }

    internal int GetSpanId(int lane, int depth, int index) => GetSpanIds().GetId(lane, depth, index);

    internal int FindSpanId(int callNodeIndex, int callIndex, TracePosition position)
    {
        if (callNodeIndex < 0)
        {
            return -1;
        }

        if (callIndex >= 0
            && GetNode(callNodeIndex) is { } node
            && FindCall(node, callIndex) is { } location
            && GetSpanId(location.ThreadIndex, location.Depth, location.Index) is var found
            && IsSpanAt(found, callNodeIndex, callIndex, position))
        {
            return found;
        }

        for (var lane = 0; lane < Threads.Count; lane++)
        {
            var rows = Threads[lane].Rows;

            for (var depth = rows.Count - 1; depth >= 0; depth--)
            {
                if (rows[depth].GetIndex(position) is >= 0 and var index
                    && rows[depth].GetCallNodeIndex(index) == callNodeIndex
                    && rows[depth].GetCallIndex(index) == callIndex)
                {
                    return GetSpanId(lane, depth, index);
                }
            }
        }

        return -1;
    }

    internal bool IsSpanAt(int spanId, int callNodeIndex, int callIndex, TracePosition position)
        => GetSpanIds().TryGet(spanId, out var row, out var index)
           && row.GetCallNodeIndex(index) == callNodeIndex
           && row.GetCallIndex(index) == callIndex
           && row.GetFromPosition(index) <= position
           && position < row.GetToPosition(index);

    internal (int CallNodeIndex, int CallIndex) GetSpanCall(int spanId)
        => GetSpanIds().TryGet(spanId, out var row, out var index) ? (row.GetCallNodeIndex(index), row.GetCallIndex(index)) : (-1, -1);

    internal ulong GetInstructions(int spanId, TracePosition position)
        => GetSpanIds().TryGet(spanId, out var row, out var index) ? GetInstructions(row.GetSpan(index), position) : 0;

    internal ulong GetInstructions(TraceSpan span, TracePosition position)
    {
        var from = Scale.GetValue(span.From.Position);

        var to = Scale.GetValue(span.To.Position);

        var fraction = to > from ? Math.Clamp((Scale.GetValue(position) - from) / (to - from), 0, 1) : 0;

        return span.From.Instructions + (ulong)((span.To.Instructions - span.From.Instructions) * fraction);
    }

    private TraceSpanIdIndex GetSpanIds() => SpanIds ??= TraceSpanIdIndex.Build(Threads);
}
