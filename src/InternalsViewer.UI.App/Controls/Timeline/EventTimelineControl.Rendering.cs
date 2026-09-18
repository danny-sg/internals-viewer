using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using InternalsViewer.UI.App.Controls.Timeline.Renderers;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Timeline;

public sealed partial class EventTimelineControl
{
    private const float RulerStripHeight = 18f;
    private const float HandleStripHeight = 16f;
    private const float MarkerStripHeight = RulerStripHeight + HandleStripHeight;
    private const float HandleHeight = 8f;
    private const float HandleGap = 13f;
    private const float TriangleHalfWidth = 9f;
    private const float MinBandLabelWidth = 36f;
    private const float BandLabelGutterPadding = 6f;
    private const float BandPadding = 2f;

    private readonly SKColor _bandColour = new(30, 30, 30, 220);

    private readonly SKColor _alternateBandColour = new(44, 44, 44, 220);

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

        var bandsTop = MarkerStripHeight;

        var bandsHeight = h - bandsTop;

        var bands = _bands.Active;

        var bandCount = bands.Count;

        var heldBand = HeldBand(bands);

        var bandHeights = TimelineBandLayout.Resolve(bands,
                                                     bandsHeight,
                                                     heldBand,
                                                     heldBand >= 0 ? bands[heldBand].MinInnerHeight + BandPadding * 2 : 0f);

        var bandTops = new float[bandCount];

        var totalTop = bandsTop;

        for (var r = 0; r < bandCount; r++)
        {
            bandTops[r] = totalTop;
            totalTop += bandHeights[r];
        }

        var frame = BuildFrame(bandTops, bandHeights);

        _timelineRenderer.DrawBands(canvas, frame);

        if (bandCount == 0)
        {
            _timelineRenderer.DrawEmpty(canvas, frame, bandsTop, bandsHeight);
        }

        _hitRegions.Clear();

        if (_definition.Events.Count == 0)
        {
            return recorder.EndRecording();
        }

        canvas.Save();

        canvas.ClipRect(new SKRect(BandLabelWidth, 0, w, h));

        var operatorBars = BuildOperatorBars(bandTops, bandHeights);

        // Traces first so the operator bars paint over them (the rails drop from a bar's edge).
        _traceRenderer.Draw(canvas, frame, operatorBars);

        _markerRenderer.Draw(canvas, frame);

        _lockRenderer.Draw(canvas, frame);

        _operatorRenderer.Draw(canvas, frame, operatorBars);

        _timelineRenderer.DrawRuler(canvas, frame);

        canvas.Restore();

        return recorder.EndRecording();
    }

    /// <remarks>
    /// Snapshots the per-paint data and geometry the lane renderers draw from: the event data, this frame's row layout, and the current
    /// zoom/scroll captured in TimeToX.
    /// </remarks>
    private TimelineFrame BuildFrame(float[] bandTops, float[] bandHeights) => new()
    {
        Times = _times,
        Bands = _bands,
        Definition = _definition,
        BandTops = bandTops,
        BandHeights = bandHeights,
        CanvasWidth = CanvasWidth,
        BandLabelWidth = BandLabelWidth,
        BandPadding = BandPadding,
        AxisUnitsPerMs = AxisUnitsPerMs,
        TimeToX = TimeToX,
        BandMarkerWidth = BandMarkerWidth,
        ColourProvider = ColourProvider,
        ShowThreads = _showThreads,
        BandColour = _bandColour,
        AlternateBandColour = _alternateBandColour,
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
        if (_definition.Events.Count == 0)
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
                                          BandLabelWidth);

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

    private static int HeldBand(IReadOnlyList<TimelineBand> bands)
    {
        for (var r = 0; r < bands.Count; r++)
        {
            if (bands[r].MinInnerHeight > 0)
            {
                return r;
            }
        }

        return -1;
    }

    private float BandMarkerWidth(int bandIndex) => _bands.IsSparse(bandIndex) ? SparseMarkerWidth : MarkerWidth;

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
