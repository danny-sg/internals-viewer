using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.Query.FullTrace.Tests;

internal static class TraceTickSeriesHelper
{
    public static IEnumerable<TraceTick> GetAllTicks(this TraceTickSeries series) => series.AllIndexes.SelectMany(i => i.All);
}
