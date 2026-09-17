using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events;
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
    public required IReadOnlyList<EngineEvent> Events { get; init; }

    /// <summary>
    /// Effective (min-relative) event start time in milliseconds, aligned index-for-index with Events.
    /// </summary>
    public required IReadOnlyList<double> Times { get; init; }

    public required TimelineRowSet Rows { get; init; }

    public required SegmentScanLanes SegmentLanes { get; init; }

    public required ObjectPoolLanes PoolLanes { get; init; }

    public required float[] RowTops { get; init; }

    public required float[] RowHeights { get; init; }

    public required float CanvasWidth { get; init; }

    public required float RowLabelWidth { get; init; }

    public required float RowPadding { get; init; }

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
    public required Func<int, float> RowMarkerWidth { get; init; }

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
    public required SKColor LaneColour { get; init; }

    /// <summary>
    /// Alternating row-background colour (odd rows)
    /// </summary>
    public required SKColor AlternateLaneColour { get; init; }

    /// <summary>
    /// The axis origin in milliseconds, and the 
    /// </summary>
    public required double MinTime { get; init; }

    /// <summary>
    /// Inverse of TimeToX, for the ruler's tick placement
    /// </summary>
    public required Func<double, double> XToTime { get; init; }
}
