using System;
using System.Linq;
using InternalsViewer.UI.App.Controls.Timeline.Renderers;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    private const float RulerBandHeight = 18f;
    private const float HandleBandHeight = 16f;
    private const float MarkerStripHeight = RulerBandHeight + HandleBandHeight;
    private const float HandleHeight = 8f;
    private const float HandleGap = 13f;
    private const float TriangleHalfWidth = 9f;
    private const float MinRowLabelWidth = 36f;
    private const float RowLabelGutterPadding = 6f;
    private const float RowPadding = 2f;

    private readonly SKColor _laneColour = new(30, 30, 30, 220);

    private readonly SKColor _alternateLaneColour = new(44, 44, 44, 220);

    private SKPicture? _staticLayer;

    private StaticLayerKey _staticLayerKey;

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        var w = e.Info.Width;
        var h = e.Info.Height;

        if (w <= 0 || h <= 0)
        {
            return;
        }

        var key = BuildStaticLayerKey(w, h);

        if (_staticLayer is null || !key.Equals(_staticLayerKey))
        {
            _staticLayer?.Dispose();
            _staticLayer = RecordStaticLayer(w, h);
            _staticLayerKey = key;
        }

        canvas.DrawPicture(_staticLayer);

        DrawDynamicOverlay(canvas, w, h);
    }

    /// <summary>
    /// Static freeze record picture of the timeline
    /// </summary>
    /// <remarks>
    /// Changes on zoom, scroll, resize, or operator selection
    /// </remarks>
    private SKPicture RecordStaticLayer(int w, int h)
    {
        using var recorder = new SKPictureRecorder();

        var canvas = recorder.BeginRecording(new SKRect(0, 0, w, h));

        var rowsTop = MarkerStripHeight;

        var rowsHeight = h - rowsTop;

        var rows = _rows.Active;

        var rowCount = rows.Count;

        var totalWeight = rows.Sum(r => r.Weight);

        var rowTops = new float[rowCount];

        var rowHeights = new float[rowCount];

        var totalTop = rowsTop;

        for (var r = 0; r < rowCount; r++)
        {
            rowTops[r] = totalTop;
            rowHeights[r] = rowsHeight * rows[r].Weight / totalWeight;
            totalTop += rowHeights[r];
        }

        var frame = BuildFrame(rowTops, rowHeights);

        _timelineRenderer.DrawRows(canvas, frame);

        _hitRegions.Clear();

        if (_sortedEvents.Count == 0)
        {
            return recorder.EndRecording();
        }

        canvas.Save();

        canvas.ClipRect(new SKRect(RowLabelWidth, 0, w, h));

        _markerRenderer.Draw(canvas, frame);

        _lockRenderer.Draw(canvas, frame);

        var operatorBars = BuildOperatorBars(rowTops, rowHeights);

        // Traces first so the operator bars paint over them (the rails drop from a bar's edge).
        _traceRenderer.Draw(canvas, frame, operatorBars);

        _operatorRenderer.Draw(canvas, frame, operatorBars);

        _timelineRenderer.DrawRuler(canvas, frame);

        canvas.Restore();

        return recorder.EndRecording();
    }

    // Snapshots the per-paint data and geometry the lane renderers draw from: the event data, this frame's row layout,
    // and the current zoom/scroll captured in TimeToX.
    private TimelineFrame BuildFrame(float[] rowTops, float[] rowHeights) => new()
    {
        Events = _sortedEvents,
        Times = _times,
        Rows = _rows,
        RowTops = rowTops,
        RowHeights = rowHeights,
        CanvasWidth = CanvasWidth,
        RowLabelWidth = RowLabelWidth,
        RowPadding = RowPadding,
        AxisUnitsPerMs = AxisUnitsPerMs,
        TimeToX = TimeToX,
        RowMarkerWidth = RowMarkerWidth,
        ColourProvider = ColourProvider,
        ShowThreads = _showThreads,
        LaneColour = _laneColour,
        AlternateLaneColour = _alternateLaneColour,
        MinTime = _minTime,
        XToTime = XToTime,
    };

    /// <summary>
    /// Draws the parts that move independently of the cached static layer
    /// </summary>
    /// <remarks>
    /// Includes the from/to selection dim, the range handles and the playhead
    /// </remarks>
    private void DrawDynamicOverlay(SKCanvas canvas, int w, int h)
    {
        if (_sortedEvents.Count == 0)
        {
            return;
        }

        var overlay = new TimelineOverlay(SelectionActive,
                                          Math.Min(TimeToX(_startTime), TimeToX(_endTime)),
                                          Math.Max(TimeToX(_startTime), TimeToX(_endTime)),
                                          StartDrawX,
                                          EndDrawX,
                                          PlayheadX,
                                          EffectiveToMs(_playheadTime),
                                          RowLabelWidth);

        _overlayRenderer.Draw(canvas, w, h, overlay);
    }

    private StaticLayerKey BuildStaticLayerKey(int w, int h) => new(_zoom,
                                                                    _scrollX,
                                                                    w,
                                                                    h,
                                                                    _selection.NodeId ?? int.MinValue,
                                                                    _showThreads,
                                                                    _minTime,
                                                                    _timeRange,
                                                                    _eventsVersion);

    private float RowMarkerWidth(int rowIndex) => _rows.IsSparse(rowIndex) ? SparseMarkerWidth : MarkerWidth;

    private readonly record struct StaticLayerKey(double Zoom,
                                                  double ScrollX,
                                                  int Width,
                                                  int Height,
                                                  int SelectedNodeId,
                                                  bool ShowThreads,
                                                  double MinTime,
                                                  double TimeRange,
                                                  int EventsVersion);
}
