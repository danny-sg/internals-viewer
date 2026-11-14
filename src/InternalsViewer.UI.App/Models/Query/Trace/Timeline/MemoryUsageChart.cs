using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record MemoryUsageChart(IReadOnlyList<MemoryUsageSeries> Series, ulong Peak, double Playhead);
