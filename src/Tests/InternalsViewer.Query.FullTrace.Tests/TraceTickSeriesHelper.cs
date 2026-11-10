using InternalsViewer.Query.FullTrace.Timeline.Ticks;

namespace InternalsViewer.Query.FullTrace.Tests;

internal static class TraceTickSeriesHelper
{
    public static IEnumerable<TraceTick> GetAllTicks(this TraceTickSeries series) => series.AllIndexes.SelectMany(i => i.All);

    public static int[] GetValueCounts(this TraceTickSeries series)
    {
        var counts = new int[byte.MaxValue + 1];

        foreach (var index in series.AllIndexes)
        {
            for (var position = 0; position < index.Count; position++)
            {
                counts[index.GetValue(position)]++;
            }
        }

        return counts;
    }
}
