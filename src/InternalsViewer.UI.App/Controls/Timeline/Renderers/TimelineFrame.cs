using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

/// <summary>
/// Everything a renderer needs for a single paint pass: the event data, the row layout, and the geometry that maps an
/// event time to an x-coordinate
/// </summary>
/// <remarks>
/// Rebuilt each frame from the control's current zoom/scroll/size — the frame-varying values a renderer would otherwise
/// reach back into the control for. Long-lived native resources live in <see cref="RenderResource"/>, not here.
/// </remarks>
internal sealed class TimelineFrame
{
    /// <summary>
    /// Effective (min-relative) event start time in milliseconds, aligned index-for-index with Events.
    /// </summary>
    public required IReadOnlyList<double> Times { get; init; }

    public required TimelineBandSet Bands { get; init; }

    public required TimelineDefinition Definition { get; init; }

    public required float[] BandTops { get; init; }

    public required float[] BandHeights { get; init; }

    public required float CanvasWidth { get; init; }

    public required float BandLabelWidth { get; init; }

    public required float BandPadding { get; init; }

    /// <summary>
    /// Microseconds per millisecond: EngineEvent times are microseconds, the axis works in milliseconds.
    /// </summary>
    public required double AxisUnitsPerMs { get; init; }

    /// <summary>
    /// Maps an effective time in milliseconds to its x-coordinate, capturing this frame's zoom and scroll.
    /// </summary>
    public required Func<double, float> TimeToX { get; init; }

    /// <summary>
    /// The tick width for a row: wider on sparse rows so their few events stay visible.
    /// </summary>
    public required Func<int, float> BandMarkerWidth { get; init; }

    /// <summary>
    /// The per-event/-object colour source, when one is set; null falls back to the flat lane colour.
    /// </summary>
    public required EventColourProvider? ColourProvider { get; init; }

    /// <summary>
    /// Whether parallel operators overlay their worker threads on the bar
    /// </summary>
    public required bool ShowThreads { get; init; }

    /// <summary>
    /// Alternating row-background colour (even rows)
    /// </summary>
    public required SKColor BandColour { get; init; }

    /// <summary>
    /// Alternating row-background colour (odd rows)
    /// </summary>
    public required SKColor AlternateBandColour { get; init; }

    /// <summary>
    /// The axis origin in milliseconds, and the 
    /// </summary>
    public required double MinTime { get; init; }

    /// <summary>
    /// Inverse of TimeToX, for the ruler's tick placement
    /// </summary>
    public required Func<double, double> XToTime { get; init; }

    public SKColor BaseColour(int eventIndex)
    {
        var item = Definition.Items[eventIndex];

        return item.ColourSource == TimelineColourSource.Provider && ColourProvider is { } colours
            ? colours.GetColour(Definition.Events[eventIndex]).ToSkColor().WithAlpha(255)
            : item.Colour;
    }

    public bool TryGetMarkerBounds(int eventIndex, out float top, out float height)
    {
        if (!TryGetTrackBounds(eventIndex, out top, out var trackHeight))
        {
            height = 0;

            return false;
        }

        height = Math.Max(2f, trackHeight - Definition.Items[eventIndex].Gap);

        return true;
    }

    public bool TryGetTrackBounds(int eventIndex, out float top, out float height)
    {
        top = 0;
        height = 0;

        if (eventIndex < 0 || eventIndex >= Definition.Items.Length)
        {
            return false;
        }

        var item = Definition.Items[eventIndex];

        if (item.Band < 0 || item.Fill == TimelineFill.None)
        {
            return false;
        }

        height = (BandHeights[item.Band] - BandPadding * 2) / item.TrackCount;

        top = BandTops[item.Band] + BandPadding + item.Track * height;

        return true;
    }
}
