using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed record MemoryUsageSeries(string Name, Colour Colour, ulong[] Values, bool IsTotal);
