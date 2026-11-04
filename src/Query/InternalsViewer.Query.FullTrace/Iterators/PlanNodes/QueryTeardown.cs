using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal readonly record struct QueryTeardown(uint Thread, TracePoint End)
{
    private const string TeardownSymbol = $"{IteratorSymbolLookup.QueryScanClass}::{IteratorSymbolLookup.TeardownMethod}";

    public static QueryTeardown[] GetTeardowns(TraceTimeline timeline)
        => [.. timeline.GetCalls(TeardownSymbol)
                       .Where(c => (c.Span.Flags & TraceSpanFlags.Returned) != 0)
                       .Select(c => new QueryTeardown(c.ThreadId, c.Span.To))
                       .OrderBy(t => t.End.Position)];
}
