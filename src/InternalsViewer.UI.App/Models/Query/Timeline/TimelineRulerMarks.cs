using InternalsViewer.UI.App.Models.Query.Timeline.Enums;
using SkiaSharp;

namespace InternalsViewer.UI.App.Models.Query.Timeline;

public sealed record TimelineRulerMarks(TimelineRulerMarkType Type,
                                        string Name,
                                        double[] Starts,
                                        double[] Ends,
                                        SKColor Colour,
                                        float MinimumWidth)
{
    public SKColor[]? Colours { get; init; }

    public SKColor GetColour(int index) => Colours is { } colours ? colours[index] : Colour;
}
