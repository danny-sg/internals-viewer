using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.Controls.Timeline.Renderers;

internal class TimelineRenderer: IDisposable
{
    private const double MinZoom = 1.0;

    private double _zoom = MinZoom;
    private double _scrollX;
    private int? _selectedNodeId;
    private bool _showThreads;
    private int _eventsVersion;
    private string _selectedSchema = string.Empty;
    private string _selectedTable = string.Empty;


    private const float MarkerWidth = 1f;

    private const byte LockOverlayAlpha = 150;
    private const byte FocusedDimAlpha = 70;

    public EventColourProvider ColourProvider { get; set; }
    private const float VerticalLabelPad = 1f;

    private readonly SKPaint _operatorTextPaint = new() { IsAntialias = true };

    private static readonly SKColor PlayheadColour = new(230, 60, 60);

    private static readonly SKColor HandleColour = new(95, 95, 95);

    private readonly SKPaint _playheadPaint = new()
    {
        Color = PlayheadColour,
        StrokeWidth = 2,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };

    private readonly SKPaint _playheadFill = new()
    {
        Color = PlayheadColour,
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };

    private readonly SKPaint _handlePaint = new()
    {
        Color = HandleColour,
        Style = SKPaintStyle.Fill,
        IsAntialias = true,
    };

    private readonly SKPaint _clipDimPaint = new()
    {
        Color = new SKColor(0, 0, 0, 120),
        Style = SKPaintStyle.Fill,
    };

    private readonly SKPaint _tickPaint = new()
    {
        Color = new SKColor(110, 110, 110),
        StrokeWidth = 1,
        Style = SKPaintStyle.Stroke,
        IsAntialias = false,
    };
    private readonly Canvas _overlay;
    private float DrawWidth => CanvasWidth - RowLabelWidth;
    private double ContentWidth => DrawWidth * _zoom;

    private double _playheadTime;
    private float CanvasWidth => (float)_overlay.ActualWidth;

    private const double AxisUnitsPerMs = 1000.0;

    private double _timeRange;
    private readonly TimelineRowSet _rows = new();
    private readonly SKPaint _rowBackgroundPaint = new() { Style = SKPaintStyle.Fill };
    private readonly List<(SKRect Bounds, EngineEvent Event, string? Label)> _hitRegions = [];
    private List<EngineEvent> _sortedEvents = [];
    private readonly SKFont _labelFont = new(SKTypeface.Default, 10f);
    // The selection only counts once the user has explicitly dragged a handle.

    private double EffectiveToMs(double effective) => effective - _minTime;
    private float PlayheadX => TimeToX(_playheadTime);

    private bool _selectionActivated;

    private double _startTime;

    private double _endTime;

    private bool SelectionActive => _selectionActivated;

    private readonly SKFont _operatorFont = new(SKTypeface.Default, 12f);
    private readonly SKFont _operatorBoldFont = new(SKTypeface.FromFamilyName(SKTypeface.Default.FamilyName, SKFontStyle.Bold),
        10f);

    private readonly SKPaint _labelPaint = new()
    {
        Color = SKColors.LightGray,
        IsAntialias = true,
    };

    private float TimeToX(double effectiveTimeMs)
        => RowLabelWidth + (float)((effectiveTimeMs - _minTime) / _timeRange * ContentWidth - _scrollX);

    private double XToTime(double x)
        => _minTime + (Math.Max(0, x - RowLabelWidth) + _scrollX) / ContentWidth * _timeRange;

    private double _minTime;

    private double _maxTime;

    private List<double> _times = [];

    private static double DurationMs(EngineEvent ev) => ev.DurationUs / AxisUnitsPerMs;

    private readonly SKPaint _markerPaint = new() { Style = SKPaintStyle.Fill };

    private const float SparseMarkerWidth = 4f;

    private const byte DurationOverlayAlpha = 96;

    private float StartDrawX => SelectionActive ? TimeToX(_startTime) : TimeToX(_startTime) - HandleGap;
    private float EndDrawX => SelectionActive ? TimeToX(_endTime) : TimeToX(_endTime) + HandleGap;

    ///
    /// 
    private const float RulerBandHeight = 18f;
    private const float HandleBandHeight = 16f;
    private const float MarkerStripHeight = RulerBandHeight + HandleBandHeight;
    private const float HandleWidth = 7f;
    private const float HandleHeight = 8f;
    private const float HandleGap = 13f;
    private const float TriangleHalfWidth = 9f;
    private const float RowLabelWidth = 36f;
    private const float RowPadding = 2f;

    private const float MinLabelGap = 1f;

    private SKPicture? _staticLayer;

    private StaticLayerKey _staticLayerKey;

    private readonly SKPathBuilder _pathBuilder = new();

    private readonly SKColor _laneColour = new(30, 30, 30, 220);

    private readonly SKColor _alternateLaneColour = new(44, 44, 44, 220);

    private readonly SKPaint _separatorPaint = new() { Color = new SKColor(60, 60, 60), StrokeWidth = 1 };

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

        for (var r = 0; r < rowCount; r++)
        {
            var y = rowTops[r];
            var rowHeight = rowHeights[r];

            _rowBackgroundPaint.Color = r % 2 == 0
                                        ? _laneColour
                                        : _alternateLaneColour;

            canvas.DrawRect(0, y, w, rowHeight, _rowBackgroundPaint);

            // The split Read band labels its two lanes (Buffer / Disk) when tall enough; every other row (and a Read
            // row too short for three labels) keeps its single centred, left-aligned label.
            if (rows[r].EventType != typeof(ReadEventGroup) || !TryDrawReadRowLabels(canvas, y, rowHeight))
            {
                var blob = _rows.LabelBlob(r);

                if (blob is not null)
                {
                    canvas.DrawText(blob, 2, y + rowHeight / 2 + _labelFont.Size / 2, _labelPaint);
                }
            }

            canvas.DrawLine(0, y + rowHeight, w, y + rowHeight, _separatorPaint);
        }

        _hitRegions.Clear();

        if (_sortedEvents.Count == 0)
        {
            return recorder.EndRecording();
        }

        canvas.Save();

        canvas.ClipRect(new SKRect(RowLabelWidth, 0, w, h));

        for (var i = 0; i < _sortedEvents.Count; i++)
        {
            var sourceEvent = _sortedEvents[i];

            // Locks (grouped and ungrouped) are drawn by DrawLockGroups as the category-banded Lock lane, not here.
            if (sourceEvent is ExecutionOperatorEvent or LockEvent)
            {
                continue;
            }

            var rowIndex = _rows.IndexOf(sourceEvent);

            if (rowIndex < 0)
            {
                continue;
            }

            var rowTop = rowTops[rowIndex];
            var innerTop = rowTop + RowPadding;
            var innerHeight = rowHeights[rowIndex] - RowPadding * 2;

            float markerTop;
            float markerHeight;

            // The Read lane holds only ReadEventGroup; nulling the category routes it through the per-node colour
            // provider (Kind-based) instead of the flat category tint used by the mixed Wait/Latch lanes.
            var category = sourceEvent is ReadEventGroup ? null : sourceEvent.Category;

            if (sourceEvent is ReadEventGroup readGroup)
            {
                // The read band is split into two lanes: cached (buffer-pool) reads on the top half, non-cached
                // (physical) reads on the bottom half.
                var laneHeight = innerHeight / 2f;

                markerTop = innerTop + (readGroup.ReadType == ReadType.Cached ? 0f : laneHeight);
                markerHeight = Math.Max(2f, laneHeight - 1f);
            }
            else if (category.HasValue)
            {
                var stepHeight = innerHeight / EventCategoryClassifier.CategoryCount;
                var step = (int)category.Value;

                markerTop = innerTop + step * stepHeight;
                markerHeight = Math.Max(2f, stepHeight - 1f);
            }
            else
            {
                markerTop = innerTop;
                markerHeight = innerHeight;
            }

            var markerColor = GetMarkerColor(sourceEvent, rowIndex, category);

            var markerWidth = RowMarkerWidth(rowIndex);

            var startX = TimeToX(_times[i]);

            var hasDuration = sourceEvent.DurationUs > 0;

            var endX = hasDuration
                ? TimeToX(_times[i] + DurationMs(sourceEvent))
                : startX + markerWidth;

            if (hasDuration && endX < startX + markerWidth)
            {
                endX = startX + markerWidth;
            }

            if (endX < RowLabelWidth - SparseMarkerWidth || startX > w)
            {
                continue;
            }

            if (hasDuration)
            {
                _markerPaint.Color = markerColor.WithAlpha(Math.Min(markerColor.Alpha, DurationOverlayAlpha));

                canvas.DrawRect(startX, markerTop, endX - startX, markerHeight, _markerPaint);
            }

            _markerPaint.Color = markerColor;

            // A read is considered actioned at its end (the row is returned there), so its solid tick sits at the end
            // edge to line up with the solid return rail; other lanes keep the tick at the event's start.
            var tickX = sourceEvent is ReadEventGroup && hasDuration ? endX - markerWidth : startX;

            canvas.DrawRect(tickX, markerTop, markerWidth, markerHeight, _markerPaint);

            _hitRegions.Add((new SKRect(startX - 3, markerTop, endX + 3, markerTop + markerHeight), sourceEvent, null));
        }

        //DrawLockGroups(canvas, rowTops, rowHeights);

        //DrawOperatorLines(canvas, rowTops, rowHeights);

        DrawRuler(canvas);

        canvas.Restore();

        return recorder.EndRecording();
    }

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

        var rowsTop = MarkerStripHeight;
        var rowsHeight = h - rowsTop;

        canvas.Save();

        canvas.ClipRect(new SKRect(RowLabelWidth, 0, w, h));

        if (SelectionActive)
        {
            var lo = Math.Min(TimeToX(_startTime), TimeToX(_endTime));
            var hi = Math.Max(TimeToX(_startTime), TimeToX(_endTime));

            if (lo > RowLabelWidth)
            {
                canvas.DrawRect(RowLabelWidth, rowsTop, lo - RowLabelWidth, rowsHeight, _clipDimPaint);
            }

            if (hi < w)
            {
                canvas.DrawRect(hi, rowsTop, w - hi, rowsHeight, _clipDimPaint);
            }
        }

        DrawHandle(canvas, StartDrawX, isStart: true);
        DrawHandle(canvas, EndDrawX, isStart: false);

        var px = PlayheadX;

        canvas.DrawLine(px, MarkerStripHeight, px, h, _playheadPaint);

        DrawPlayheadTriangle(canvas, px);

        DrawPlayheadTimeBadge(canvas, px);

        canvas.Restore();
    }

    private StaticLayerKey BuildStaticLayerKey(int w, int h) => new(_zoom,
                                                                    _scrollX,
                                                                    w,
                                                                    h,
                                                                    _selectedNodeId ?? int.MinValue,
                                                                    _showThreads,
                                                                    _minTime,
                                                                    _timeRange,
                                                                    _eventsVersion);

    private readonly record struct StaticLayerKey(double Zoom,
                                                  double ScrollX,
                                                  int Width,
                                                  int Height,
                                                  int SelectedNodeId,
                                                  bool ShowThreads,
                                                  double MinTime,
                                                  double TimeRange,
                                                  int EventsVersion);

    private void DrawRuler(SKCanvas canvas)
    {
        var leftMs = EffectiveToMs(XToTime(RowLabelWidth));
        var rightMs = EffectiveToMs(XToTime(CanvasWidth));

        var rangeMs = rightMs - leftMs;

        if (rangeMs <= 0)
        {
            return;
        }

        var targetTicks = Math.Max(2, DrawWidth / 80f);

        var interval = TimelineFormat.NiceInterval(rangeMs / targetTicks);

        if (interval <= 0)
        {
            return;
        }

        Span<char> textBuffer = stackalloc char[12];

        for (var tickMs = Math.Ceiling(leftMs / interval) * interval; tickMs <= rightMs; tickMs += interval)
        {
            var x = TimeToX(_minTime + tickMs);

            canvas.DrawLine(x, RulerBandHeight - 4, x, RulerBandHeight, _tickPaint);

            textBuffer.Clear();

            var length = TimelineFormat.FormatTimeIntoSpan(tickMs, textBuffer);

            using var blob = SKTextBlob.Create(textBuffer[..length], _labelFont, SKPoint.Empty);

            if (blob is not null)
            {
                canvas.DrawText(blob, x + 2, RulerBandHeight - 6, _labelPaint);
            }
        }
    }

    private void DrawPlayheadTimeBadge(SKCanvas canvas, float px)
    {
        Span<char> buf = stackalloc char[12];

        var textLength = TimelineFormat.FormatTimeIntoSpan(EffectiveToMs(_playheadTime), buf);

        var text = buf[..textLength];

        const float padding = 4f;

        var badgeWidth = _labelFont.MeasureText(text) + padding * 2;

        const float badgeHeight = RulerBandHeight - 2;

        var bx = Math.Clamp(px - badgeWidth / 2f, RowLabelWidth, Math.Max(RowLabelWidth, CanvasWidth - badgeWidth));

        canvas.DrawRoundRect(new SKRect(bx, 0, bx + badgeWidth, badgeHeight), 2, 2, _playheadFill);

        _operatorTextPaint.Color = SKColors.White;

        var baseline = badgeHeight / 2f + _labelFont.Size * 0.35f;

        using var blob = SKTextBlob.Create(text, _labelFont, SKPoint.Empty);

        if (blob is not null)
        {
            canvas.DrawText(blob, bx + padding, baseline, _operatorTextPaint);
        }
    }

    private void DrawHandle(SKCanvas canvas, float x, bool isStart)
    {
        var top = MarkerStripHeight - HandleHeight;
        var half = HandleWidth / 2f;

        _pathBuilder.MoveTo(x - half, top);
        _pathBuilder.LineTo(x + half, top);
        _pathBuilder.LineTo(isStart ? x - half : x + half, MarkerStripHeight);
        _pathBuilder.Close();

        using var path = _pathBuilder.Detach();

        canvas.DrawPath(path, _handlePaint);
    }

    private void DrawPlayheadTriangle(SKCanvas canvas, float x)
    {
        _pathBuilder.MoveTo(x, MarkerStripHeight);
        _pathBuilder.LineTo(x - TriangleHalfWidth, RulerBandHeight);
        _pathBuilder.LineTo(x + TriangleHalfWidth, RulerBandHeight);
        _pathBuilder.Close();

        using var path = _pathBuilder.Detach();

        canvas.DrawPath(path, _playheadFill);
    }

    // Draws the split Read row's three labels — "Buffer" top-aligned (the cached lane), "Disk" bottom-aligned (the
    // physical lane), "Read" centred — all left-aligned (x=2/4) like the single-label rows. Returns false (drawing
    // nothing) when the row is too short to fit all three with at least a 1px gap between them, so the caller falls
    // back to plain "Read".
    private bool TryDrawReadRowLabels(SKCanvas canvas, float rowTop, float rowHeight)
    {
        var metrics = _labelFont.Metrics;

        var textHeight = metrics.Descent - metrics.Ascent;

        if (rowHeight < textHeight * 3 + MinLabelGap * 2 + VerticalLabelPad * 2)
        {
            return false;
        }

        canvas.DrawText("Buffer", 4, rowTop + VerticalLabelPad - metrics.Ascent, SKTextAlign.Left, _labelFont, _labelPaint);

        canvas.DrawText("Read", 2, rowTop + rowHeight / 2 - (metrics.Ascent + metrics.Descent) / 2,
                        SKTextAlign.Left, _labelFont, _labelPaint);

        canvas.DrawText("Disk", 4, rowTop + rowHeight - VerticalLabelPad - metrics.Descent, SKTextAlign.Left, _labelFont, _labelPaint);

        return true;
    }

    private float RowMarkerWidth(int rowIndex) => _rows.IsSparse(rowIndex) ? SparseMarkerWidth : MarkerWidth;

    private SKColor GetMarkerColor(EngineEvent sourceEvent, int rowIndex, EventCategory? category)
    {
        var colour = sourceEvent is LockEvent { LockMode: var lockMode }
                     ? TimelineColours.LockModeColour(lockMode)
                     : category.HasValue
                         ? TimelineColours.TintByCategory(_rows.Active[rowIndex].Color, (int)category.Value)
                         : ColourProvider is { } colours
                             ? colours.GetColour(sourceEvent).ToSkColor()
                             : _rows.Active[rowIndex].Color;

        var alpha = sourceEvent is LockEvent ? LockOverlayAlpha : (byte)255;

        // Dimming an out-of-focus event only lowers the alpha, never raises it above a lock's overlay alpha.
        if (DimForSelection(sourceEvent) && FocusedDimAlpha < alpha)
        {
            alpha = FocusedDimAlpha;
        }

        return colour.WithAlpha(alpha);
    }

    private bool DimForSelection(EngineEvent ev)
    {
        if (_selectedNodeId is not { } selected)
        {
            return false;
        }

        if (ev.PlanNodeIdentifier is { } id)
        {
            return id.NodeId != selected;
        }

        if (ev is LockEvent && !string.IsNullOrEmpty(_selectedTable))
        {
            return !string.Equals(ev.TableName, _selectedTable, StringComparison.OrdinalIgnoreCase)
                   || !string.Equals(ev.SchemaName, _selectedSchema, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
    public void Dispose()
    {
     
    }
}

internal class OperatorRenderer : IDisposable
{
    public void Dispose()
    {

    }
}
