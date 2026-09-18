using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Definition;

public readonly record struct TimelineItem(int Band,
                                           int Track,
                                           int TrackCount,
                                           byte Gap,
                                           TimelineFill Fill,
                                           TimelineTickAnchor Tick,
                                           TimelineColourSource ColourSource,
                                           SKColor Colour,
                                           float MinWidth,
                                           byte Layer = 0,
                                           float StartInset = 0f)
{
    public static TimelineItem Unplaced { get; } = new(-1, 0, 1, 0, TimelineFill.None, TimelineTickAnchor.Start,
                                                       TimelineColourSource.Fixed, SKColor.Empty, 0f);

    public static TimelineItem Undrawn(int band) => Unplaced with { Band = band };
}

public enum TimelineFill : byte
{
    None,
    Translucent,
    Solid,
}

public enum TimelineTickAnchor : byte
{
    Start,
    End,
}

public enum TimelineColourSource : byte
{
    Fixed,
    Provider,
}
