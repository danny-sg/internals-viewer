using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float RulerHeight = 20f;

    private const float PlayheadStripHeight = 16f;

    private const float MemoryBarHeight = 30f;

    private const float PlayheadHalfWidth = 9f;

    private const float PlayheadBadgePadding = 4f;

    private const float LaneHeaderHeight = 18f;

    private const float MaximumRowHeight = 16f;

    private const float FocusedMaximumRowHeight = 32f;

    private const float MinimumRowHeight = 2f;

    private const float MinimumLabelRowHeight = 11f;

    private const float GappedRowHeight = 4f;

    private const float LaneGap = 4f;

    private const float ChevronSize = 7f;

    private const float MinimumLabelWidth = 30f;

    private const float LabelPadding = 3f;

    private const float TickSpacing = 110f;

    private const float NarrowSpan = 1f;

    private const byte RunAlpha = 190;

    private const float SliceWidth = 16f;

    private const float AllocatedBandHeight = 36f;

    private const float InUseBandHeight = 56f;

    private const float MemoryBandHeight = AllocatedBandHeight + InUseBandHeight;

    private const float MemoryBandLabelHeight = 14f;

    private const float MemoryBucketWidth = 2f;

    private const float GrantLabelGap = 3f;

    private const float MarkerRadius = 3f;

    private const float MinimumExtrusion = 3f;

    private const float DefaultExtrusionLength = 140f;

    private const float DefaultExtrusionAngle = -0.2738f;

    private const float BaseShare = 0.25f;

    private const float CapShade = 0.85f;

    private const float UnderShade = 0.55f;

    private const float SideShade = 0.68f;

    private const byte LandscapeAlpha = 200;

    private const float SpikeMinimumWidth = 3f;

    private const float SurfaceBucketWidth = 3f;

    private const float SurfaceEase = 6f;

    private float _rowHeight = MaximumRowHeight;

    private SKPicture? _staticLayer;

    private LayerKey _staticLayerKey;

    private SKPicture? _landscapeLayer;

    private LandscapeKey _landscapeLayerKey;

    private readonly List<PopoutBlock> _popoutBlocks = [];

    private readonly List<PopoutBlock> _spikes = [];

    private readonly Dictionary<FlameHit, int> _spikeIndex = [];

    private readonly List<SKPoint> _surfacePoints = [];

    private float _popoutX;

    private float _extrusionAngle = DefaultExtrusionAngle;

    private float _extrusionLength = DefaultExtrusionLength;

    private float _drawnLength = DefaultExtrusionLength;

    private float _directionX = MathF.Cos(DefaultExtrusionAngle);

    private float _directionY = MathF.Sin(DefaultExtrusionAngle);

    private float BarHeight => _rowHeight >= GappedRowHeight ? _rowHeight - 1 : _rowHeight;

    private string AxisUnit => _axis == TimeTravelTimelineAxis.Position ? "Trace Position" : "Instructions Per Thread";

    private double EdgeTolerance => (_viewEnd - _viewStart) / Math.Max(_overlay.ActualWidth, 1);

    private bool ShowsStart => _viewStart <= FullStart + EdgeTolerance;

    private bool ShowsEnd => _viewEnd >= FullEnd - EdgeTolerance;

    private double PixelsPerUnit(int width) => width / Math.Max(_viewEnd - _viewStart, double.Epsilon);

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        var width = e.Info.Width;

        var height = e.Info.Height;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var key = new LayerKey(width, height, _viewStart, _viewEnd, _scrollY, _version);

        if (_staticLayer is null || !key.Equals(_staticLayerKey))
        {
            _staticLayer?.Dispose();

            _staticLayer = RecordStaticLayer(width, height);

            _staticLayerKey = key;
        }

        canvas.DrawPicture(_staticLayer);

        if (ShowMemory)
        {
            var landscapeKey = new LandscapeKey(key, _extrusionAngle, _extrusionLength);

            if (_landscapeLayer is null || !landscapeKey.Equals(_landscapeLayerKey))
            {
                _landscapeLayer?.Dispose();

                _landscapeLayer = RecordLandscape(width, height);

                _landscapeLayerKey = landscapeKey;
            }

            canvas.DrawPicture(_landscapeLayer);
        }

        DrawOverlay(canvas, width, height);
    }

    private SKPicture RecordStaticLayer(int width, int height)
    {
        using var recorder = new SKPictureRecorder();

        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));

        if (_visible is { } visible)
        {
            var scale = PixelsPerUnit(width);

            DrawOperators(canvas, width, scale);

            canvas.Save();

            var lanesBottom = height - (int)BandHeight;

            canvas.ClipRect(new SKRect(0, ContentTop, width, lanesBottom));

            DrawLanes(canvas, visible, lanesBottom, width, scale);

            canvas.Restore();

            if (ShowsMemoryBand)
            {
                DrawMemoryBand(canvas, visible, width, height, scale);
            }

            canvas.Save();

            canvas.Translate(0, RulerTop);

            DrawRuler(canvas, width, scale);

            canvas.Restore();
        }

        return recorder.EndRecording();
    }

    private SKPicture RecordLandscape(int width, int height)
    {
        using var recorder = new SKPictureRecorder();

        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));

        _spikes.Clear();

        _spikeIndex.Clear();

        _hoverBlock = null;

        if (_visible is { } visible)
        {
            var lanesBottom = height - BandHeight;

            canvas.ClipRect(new SKRect(0, 0, width, lanesBottom));

            DrawLandscape(canvas, visible, width, lanesBottom, PixelsPerUnit(width));
        }

        return recorder.EndRecording();
    }

    private void DrawLanes(SKCanvas canvas, TimeTravelTimeline timeline, int height, int width, double scale)
    {
        for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
        {
            var thread = timeline.Threads[lane];

            var depthCount = DepthOf(thread);

            var laneTop = ContentTop + _laneTops[lane] - (float)_scrollY;

            if (laneTop + LaneHeaderHeight + depthCount * _rowHeight + LaneGap < ContentTop)
            {
                continue;
            }

            if (laneTop > height)
            {
                break;
            }

            DrawLaneHeader(canvas, thread, laneTop, width, timeline.Threads.Count > 1);

            var rowsTop = laneTop + LaneHeaderHeight;

            for (var depth = 0; depth < depthCount && depth < thread.Rows.Count; depth++)
            {
                var top = rowsTop + depth * _rowHeight;

                if (top + _rowHeight < ContentTop)
                {
                    continue;
                }

                if (top > height)
                {
                    break;
                }

                DrawRow(canvas, thread.Rows[depth], top, scale, width, RaisedOf(thread, lane, depth));
            }
        }
    }

    private void DrawLaneHeader(SKCanvas canvas, TimeTravelTimelineThread thread, float top, int width, bool collapsible)
    {
        _paints.Fill.Color = _paints.LaneHeader;

        canvas.DrawRect(0, top, width, LaneHeaderHeight, _paints.Fill);

        var left = LabelPadding * 2;

        if (collapsible)
        {
            DrawChevron(canvas, left + ChevronSize / 2, top + LaneHeaderHeight / 2, IsExpanded(thread));

            left += ChevronSize + LabelPadding * 2;
        }

        canvas.DrawText($"Thread {thread.ThreadId}",
                        left,
                        Baseline(top, LaneHeaderHeight),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Label);
    }

    private void DrawChevron(SKCanvas canvas, float centreX, float centreY, bool expanded)
    {
        var half = ChevronSize / 2;

        if (expanded)
        {
            _pathBuilder.MoveTo(centreX - half, centreY - half / 2);
            _pathBuilder.LineTo(centreX + half, centreY - half / 2);
            _pathBuilder.LineTo(centreX, centreY + half / 2);
        }
        else
        {
            _pathBuilder.MoveTo(centreX - half / 2, centreY - half);
            _pathBuilder.LineTo(centreX + half / 2, centreY);
            _pathBuilder.LineTo(centreX - half / 2, centreY + half);
        }

        _pathBuilder.Close();

        using var path = _pathBuilder.Detach();

        canvas.DrawPath(path, _paints.Label);
    }

    private Func<int, bool>? RaisedOf(TimeTravelTimelineThread thread, int lane, int depth)
    {
        if (!ShowMemory || _visible is not { } timeline)
        {
            return null;
        }

        if (MemoryMode == FlameChartMemoryMode.InUse)
        {
            var row = thread.Rows[depth];

            return index =>
            {
                var positions = row.Span(TimeTravelTimelineAxis.Position, index);

                return timeline.AllocatedDuring(thread.ThreadId, positions.Start, positions.End).Count > 0;
            };
        }

        return _raisedRows.TryGetValue((lane, depth), out var raised) ? index => Array.BinarySearch(raised.Indexes, index) >= 0 : null;
    }

    private void DrawRow(SKCanvas canvas, TimeTravelTimelineRow row, float top, double scale, int width, Func<int, bool>? raised)
    {
        var starts = row.Starts(_axis);

        var ends = row.Ends(_axis);

        var runStart = -1f;

        var runEnd = -1f;

        var runColour = SKColors.Transparent;

        var index = row.FirstEndingAfter(_axis, _viewStart, 0);

        while (index < row.Count && starts[index] <= _viewEnd)
        {
            var left = (float)((starts[index] - _viewStart) * scale);

            var right = (float)((ends[index] - _viewStart) * scale);

            if (right - left >= NarrowSpan)
            {
                DrawRun(canvas, runStart, runEnd, runColour, top);

                runStart = -1f;

                var labelled = raised is null || !raised(index);

                DrawSpan(canvas, row.NodeAt(index), left, right, top, width, labelled);

                index++;

                continue;
            }

            if (runStart < 0 || left > runEnd + NarrowSpan)
            {
                DrawRun(canvas, runStart, runEnd, runColour, top);

                runStart = left;

                runColour = ColourOf(row.NodeAt(index));
            }

            var boundary = MathF.Floor(Math.Max(right, left)) + 1f;

            runEnd = boundary;

            index = row.FirstEndingAfter(_axis, _viewStart + boundary / scale, index + 1);
        }

        DrawRun(canvas, runStart, runEnd, runColour, top);
    }

    private void DrawRun(SKCanvas canvas, float start, float end, SKColor colour, float top)
    {
        if (start < 0)
        {
            return;
        }

        _paints.Fill.Color = colour.WithAlpha(RunAlpha);

        canvas.DrawRect(start, top, Math.Max(end - start, NarrowSpan), BarHeight, _paints.Fill);
    }

    private void DrawSpan(SKCanvas canvas, int node, float left, float right, float top, int width, bool labelled)
    {
        var colour = ColourOf(node);

        var visibleLeft = Math.Max(left, -1f);

        var visibleRight = Math.Min(right, width + 1f);

        var rect = new SKRect(visibleLeft, top, Math.Max(visibleRight - 1f, visibleLeft + NarrowSpan), top + BarHeight);

        _paints.Fill.Color = colour;

        canvas.DrawRect(rect, _paints.Fill);

        if (!labelled || rect.Width < MinimumLabelWidth || _rowHeight < MinimumLabelRowHeight)
        {
            return;
        }

        _paints.Text.Color = IsLight(colour) ? SKColors.Black : SKColors.White;

        canvas.Save();

        canvas.ClipRect(rect);

        canvas.DrawText(LabelOf(node),
                        Math.Max(rect.Left, 0) + LabelPadding,
                        Baseline(top, BarHeight),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Text);

        canvas.Restore();
    }

    private void DrawRuler(SKCanvas canvas, int width, double scale)
    {
        var unitLeft = width - _paints.Font.MeasureText(AxisUnit) - LabelPadding * 4;

        var step = TickStep(TickSpacing / scale);

        var first = Math.Ceiling(Math.Max(_viewStart, FullStart) / step) * step;

        for (var value = first; value <= Math.Min(_viewEnd, FullEnd); value += step)
        {
            var x = (float)((value - _viewStart) * scale);

            canvas.DrawLine(x, RulerHeight - 6, x, RulerHeight, _paints.Tick);

            var label = FormatAxis(value, step);

            if (x + 3 + _paints.Font.MeasureText(label) < unitLeft)
            {
                canvas.DrawText(label, x + 3, Baseline(0, RulerHeight - 4), SKTextAlign.Left, _paints.Font, _paints.Label);
            }
        }

        canvas.DrawLine(0, RulerHeight - 0.5f, width, RulerHeight - 0.5f, _paints.Tick);

        canvas.DrawText(AxisUnit, width - LabelPadding * 2, Baseline(0, RulerHeight - 4), SKTextAlign.Right, _paints.Font, _paints.Label);
    }

    private void DrawOverlay(SKCanvas canvas, int width, int height)
    {
        var hover = _hoverOnPopout ? null : _hover;

        var selected = IsLocked ? null : _selected;

        canvas.Save();

        canvas.ClipRect(new SKRect(0, ContentTop, width, height - BandHeight));

        DrawSelectedCalls(canvas, width, height);

        DrawHighlight(canvas, hover, width, _paints.Hover);

        DrawHighlight(canvas, selected, width, _paints.Selection);

        canvas.Restore();

        DrawOperatorOverlay(canvas, width);

        if (ShowsSpikes)
        {
            canvas.Save();

            canvas.ClipRect(new SKRect(0, 0, width, height - BandHeight));

            DrawSpikeHighlight(canvas, hover, _paints.Hover);

            DrawSpikeHighlight(canvas, selected, _paints.Selection);

            canvas.Restore();
        }

        DrawPopout(canvas, width, height);

        DrawHover(canvas);

        if (_isSelecting)
        {
            var rect = SKRect.Create((float)Math.Min(_pressPoint.X, _dragPoint.X),
                                     (float)Math.Min(_pressPoint.Y, _dragPoint.Y),
                                     (float)Math.Abs(_dragPoint.X - _pressPoint.X),
                                     (float)Math.Abs(_dragPoint.Y - _pressPoint.Y));

            canvas.DrawRect(rect, _paints.DragFill);

            canvas.DrawRect(rect, _paints.DragStroke);
        }
    }

    private void DrawPopout(SKCanvas canvas, int width, int height)
    {
        _popoutBlocks.Clear();

        if (_playhead is not { } value || _visible is not { } timeline)
        {
            return;
        }

        var scale = PixelsPerUnit(width);

        var x = (float)((value - _viewStart) * scale);

        if (x < 0 || x > width)
        {
            return;
        }

        _popoutX = x;

        canvas.DrawLine(x, ContentTop, x, height, _paints.Playhead);

        DrawBandMarker(canvas, timeline, x, width, height, scale);

        if (ShowMemory)
        {
            DrawPlayheadHandle(canvas, x, value, width);

            return;
        }

        canvas.Save();

        canvas.ClipRect(new SKRect(0, 0, width, height - BandHeight));

        for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
        {
            var thread = timeline.Threads[lane];

            var laneTop = ContentTop + _laneTops[lane] - (float)_scrollY;

            if (laneTop > height)
            {
                break;
            }

            var inherited = 0f;

            for (var depth = 0; depth < DepthOf(thread) && depth < thread.Rows.Count; depth++)
            {
                var top = laneTop + LaneHeaderHeight + depth * _rowHeight;

                if (top > height)
                {
                    break;
                }

                var row = thread.Rows[depth];

                var index = row.IndexAt(_axis, value, 0);

                if (index < 0)
                {
                    continue;
                }

                var span = row.Span(_axis, index);

                var positions = row.Span(TimeTravelTimelineAxis.Position, index);

                var (bytes, _) = timeline.AllocatedDuring(thread.ThreadId, positions.Start, positions.End);

                var spanLeft = (float)((span.Start - _viewStart) * scale);

                var spanRight = (float)((span.End - _viewStart) * scale);

                var node = row.NodeAt(index);

                var ratio = RatioOf(bytes);

                var inheriting = bytes == 0 && inherited > 0;

                inherited = inheriting ? inherited : ratio;

                if (top + _rowHeight < ContentTop)
                {
                    continue;
                }

                _popoutBlocks.Add(new PopoutBlock(node,
                                                  ColourOf(node),
                                                  spanLeft,
                                                  spanRight,
                                                  x,
                                                  x,
                                                  top,
                                                  inherited,
                                                  0,
                                                  bytes,
                                                  false,
                                                  new FlameHit(lane, depth, index),
                                                  BarHeight));
            }
        }

        Orient(width, height - BandHeight);

        for (var index = 0; index < _popoutBlocks.Count; index++)
        {
            DrawBlock(canvas, _popoutBlocks[DrawIndex(index)]);
        }

        canvas.Restore();

        DrawPlayheadHandle(canvas, x, value, width);
    }

    private void DrawLandscape(SKCanvas canvas, TimeTravelTimeline timeline, int width, float bottom, double scale)
    {
        _directionX = MathF.Cos(_extrusionAngle);

        _directionY = MathF.Sin(_extrusionAngle);

        if (MemoryMode == FlameChartMemoryMode.InUse)
        {
            CollectSurfaces(timeline, width, bottom, scale);
        }
        else
        {
            CollectSpikes(timeline, bottom, scale);
        }

        var length = Math.Min(_extrusionLength, MaximumLength(_spikes, _directionX, _directionY, width, bottom, popout: false));

        for (var index = 0; index < _spikes.Count; index++)
        {
            _spikes[index] = _spikes[index] with { Extrusion = ExtrusionOf(_spikes[index].Ratio, length) };

            _spikeIndex[_spikes[index].Source] = index;
        }

        if (MemoryMode == FlameChartMemoryMode.InUse)
        {
            DrawSurfaces(canvas);

            return;
        }

        foreach (var spike in _spikes)
        {
            DrawBlock(canvas, spike, LandscapeAlpha, named: false);

            if (spike.Named)
            {
                DrawFaceLabel(canvas, spike);
            }
        }
    }

    private void DrawFaceLabel(SKCanvas canvas, PopoutBlock block)
    {
        var front = FrontOf(block);

        if (block.Source.IsOperator)
        {
            DrawOperatorLabel(canvas, _operatorRows[block.Source.OperatorRow], front);

            return;
        }

        if (front.Width < MinimumLabelWidth || _rowHeight < MinimumLabelRowHeight)
        {
            return;
        }

        _paints.Text.Color = IsLight(block.Colour) ? SKColors.Black : SKColors.White;

        canvas.Save();

        canvas.ClipRect(front);

        canvas.DrawText(TextOf(block),
                        Math.Max(front.Left, 0) + LabelPadding,
                        Baseline(front.Top, block.Height),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Text);

        canvas.Restore();
    }

    private double PositionAt(TimeTravelTimelineRow row, int index, double value)
    {
        var positions = row.Span(TimeTravelTimelineAxis.Position, index);

        if (_axis == TimeTravelTimelineAxis.Position)
        {
            return Math.Min(value, positions.End);
        }

        var instructions = row.Span(TimeTravelTimelineAxis.Instructions, index);

        var fraction = instructions.End > instructions.Start ? (value - instructions.Start) / (instructions.End - instructions.Start) : 1;

        return positions.Start + Math.Clamp(fraction, 0, 1) * (positions.End - positions.Start);
    }

    private void DrawHover(SKCanvas canvas)
    {
        var block = _hoverOnPopout && _hover is { } hover ? PopoutOf(hover) : ShowMemory ? _hoverBlock : null;

        if (block is not { } hovered)
        {
            return;
        }

        if (_hoverOnPopout)
        {
            DrawWireframe(canvas, hovered, _paints.Hover);
        }
        else if (MemoryMode == FlameChartMemoryMode.InUse)
        {
            canvas.DrawRect(FrontOf(hovered), _paints.Hover);
        }

        if (ShowMemory && hovered.Label > 0)
        {
            DrawBlockLabel(canvas, hovered);
        }
    }

    private PopoutBlock? PopoutOf(FlameHit hit)
    {
        foreach (var block in _popoutBlocks)
        {
            if (block.Source == hit && block.Extrusion > 0)
            {
                return block;
            }
        }

        return null;
    }

    private void DrawHighlight(SKCanvas canvas, FlameHit? hit, int width, SKPaint paint)
    {
        if (hit is not { } highlighted || (ShowsSpikes && SpikeOf(highlighted) is not null))
        {
            return;
        }

        if (SpanRect(highlighted, width) is { } rect)
        {
            canvas.DrawRect(rect, paint);
        }
    }

    private void DrawSpikeHighlight(SKCanvas canvas, FlameHit? hit, SKPaint paint)
    {
        if (hit is { } highlighted && SpikeOf(highlighted) is { } spike)
        {
            DrawWireframe(canvas, spike, paint);
        }
    }

    private void DrawWireframe(SKCanvas canvas, PopoutBlock block, SKPaint paint)
    {
        var front = FrontOf(block);

        var bottom = block.Top + block.Height;

        canvas.DrawRect(block.Left, block.Top, block.Right - block.Left, block.Height, paint);

        canvas.DrawRect(front, paint);

        canvas.DrawLine(block.Left, block.Top, front.Left, front.Top, paint);

        canvas.DrawLine(block.Right, block.Top, front.Right, front.Top, paint);

        canvas.DrawLine(block.Left, bottom, front.Left, front.Bottom, paint);

        canvas.DrawLine(block.Right, bottom, front.Right, front.Bottom, paint);
    }

    private PopoutBlock? SpikeOf(FlameHit hit) => _spikeIndex.TryGetValue(hit, out var index) ? _spikes[index] : null;

    private void CollectSpikes(TimeTravelTimeline timeline, float bottom, double scale)
    {
        _spikes.Clear();

        if (_selfMaximum == 0)
        {
            return;
        }

        var rightFirst = _directionX >= 0;

        var reach = _extrusionLength * Math.Abs(_directionX) / scale;

        var rise = _extrusionLength * Math.Max(0, -_directionY);

        var from = rightFirst ? _viewStart - reach : _viewStart;

        var to = rightFirst ? _viewEnd : _viewEnd + reach;

        for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
        {
            var thread = timeline.Threads[lane];

            var rowsTop = ContentTop + _laneTops[lane] - (float)_scrollY + LaneHeaderHeight;

            for (var depth = 0; depth < DepthOf(thread) && depth < thread.Rows.Count; depth++)
            {
                var top = rowsTop + depth * _rowHeight;

                if (top - rise > bottom)
                {
                    return;
                }

                if (top + _rowHeight < ContentTop || !_raisedRows.TryGetValue((lane, depth), out var raised))
                {
                    continue;
                }

                var row = thread.Rows[depth];

                var first = Array.BinarySearch(raised.Indexes, row.FirstEndingAfter(_axis, from));

                var rowStart = _spikes.Count;

                for (var item = first < 0 ? ~first : first; item < raised.Indexes.Length; item++)
                {
                    var index = raised.Indexes[item];

                    var span = row.Span(_axis, index);

                    if (span.Start > to)
                    {
                        break;
                    }

                    var left = (float)((span.Start - _viewStart) * scale);

                    var right = Math.Max((float)((span.End - _viewStart) * scale), left + SpikeMinimumWidth);

                    var spike = Raised(new FlameHit(lane, depth, index),
                                       row.NodeAt(index),
                                       left,
                                       right,
                                       top,
                                       BarHeight,
                                       raised.Bytes[item],
                                       _selfMaximum,
                                       true);

                    var last = _spikes.Count - 1;

                    if (last >= rowStart && left < _spikes[last].Right)
                    {
                        var dominant = spike.Label > _spikes[last].Label ? spike : _spikes[last];

                        _spikes[last] = Raised(dominant.Source,
                                               dominant.Node,
                                               _spikes[last].Left,
                                               Math.Max(_spikes[last].Right, right),
                                               top,
                                               BarHeight,
                                               _spikes[last].Label + spike.Label,
                                               _selfMaximum,
                                               true);
                    }
                    else
                    {
                        _spikes.Add(spike);
                    }
                }

                if (rightFirst)
                {
                    _spikes.Reverse(rowStart, _spikes.Count - rowStart);
                }
            }
        }
    }

    private void CollectSurfaces(TimeTravelTimeline timeline, int width, float bottom, double scale)
    {
        _spikes.Clear();

        if (_inUseMaximum == 0)
        {
            return;
        }

        var rightFirst = _directionX >= 0;

        var reach = _extrusionLength * Math.Abs(_directionX);

        var rise = _extrusionLength * Math.Max(0, -_directionY);

        var firstPixel = rightFirst ? -reach : 0;

        var lastPixel = rightFirst ? width : width + reach;

        CollectOperatorSurfaces(firstPixel, lastPixel, scale, rightFirst);

        for (var lane = 0; lane < timeline.Threads.Count && lane < _laneTops.Length; lane++)
        {
            var thread = timeline.Threads[lane];

            var rowsTop = ContentTop + _laneTops[lane] - (float)_scrollY + LaneHeaderHeight;

            for (var depth = 0; depth < DepthOf(thread) && depth < thread.Rows.Count; depth++)
            {
                var top = rowsTop + depth * _rowHeight;

                if (top - rise > bottom)
                {
                    return;
                }

                if (top + _rowHeight < ContentTop)
                {
                    continue;
                }

                var row = thread.Rows[depth];

                var rowStart = _spikes.Count;

                var index = row.FirstEndingAfter(_axis, _viewStart + firstPixel / scale);

                while (index < row.Count && row.Starts(_axis)[index] <= _viewStart + lastPixel / scale)
                {
                    var span = row.Span(_axis, index);

                    var left = (float)((span.Start - _viewStart) * scale);

                    var right = (float)((span.End - _viewStart) * scale);

                    if (right - left < SurfaceBucketWidth)
                    {
                        index = row.FirstEndingAfter(_axis, _viewStart + (MathF.Floor(right) + 1) / scale, index + 1);

                        continue;
                    }

                    AddSurface(timeline,
                               thread.ThreadId,
                               row,
                               new FlameHit(lane, depth, index),
                               top,
                               Math.Max(left, firstPixel),
                               Math.Min(right, lastPixel),
                               scale);

                    index++;
                }

                if (rightFirst)
                {
                    _spikes.Reverse(rowStart, _spikes.Count - rowStart);
                }
            }
        }
    }

    private void AddSurface(TimeTravelTimeline timeline,
                            uint threadId,
                            TimeTravelTimelineRow row,
                            FlameHit source,
                            float top,
                            float left,
                            float right,
                            double scale)
    {
        var positions = row.Span(TimeTravelTimelineAxis.Position, source.Index);

        if (!_inUseCurves.TryGetValue(source, out var curve))
        {
            curve = timeline.InUseWithin(threadId, positions.Start, positions.End);

            _inUseCurves[source] = curve;
        }

        if (curve.IsEmpty)
        {
            return;
        }

        var node = row.NodeAt(source.Index);

        var needed = _paints.Font.MeasureText(LabelOf(node)) + LabelPadding * 2;

        var first = _spikes.Count;

        for (var x = left; x < right; x += SurfaceBucketWidth)
        {
            var end = Math.Min(x + SurfaceBucketWidth, right);

            var bytes = curve.PeakDuring(PositionAt(row, source.Index, _viewStart + x / scale),
                                         PositionAt(row, source.Index, _viewStart + end / scale));

            var last = _spikes.Count - 1;

            if (last >= first && _spikes[last].Label == bytes && _spikes[last].Right >= x)
            {
                _spikes[last] = _spikes[last] with { SpanRight = end, Right = end };
            }
            else
            {
                _spikes.Add(Raised(source, node, x, end, top, BarHeight, bytes, _inUseMaximum, false));
            }
        }

        FinishSurface(first, needed);
    }

    private void FinishSurface(int first, float needed)
    {
        var widest = -1;

        var raised = false;

        for (var index = first; index < _spikes.Count; index++)
        {
            if (_spikes[index].Label == 0)
            {
                continue;
            }

            raised = true;

            var slab = _spikes[index].Right - _spikes[index].Left;

            if (slab >= needed && (widest < 0 || slab > _spikes[widest].Right - _spikes[widest].Left))
            {
                widest = index;
            }
        }

        if (!raised)
        {
            _spikes.RemoveRange(first, _spikes.Count - first);

            return;
        }

        if (widest >= 0)
        {
            _spikes[widest] = _spikes[widest] with { Named = true };
        }
    }

    private void DrawSurfaces(SKCanvas canvas)
    {
        var first = 0;

        while (first < _spikes.Count)
        {
            var end = first + 1;

            while (end < _spikes.Count && _spikes[end].Source == _spikes[first].Source)
            {
                end++;
            }

            DrawSurface(canvas, first, end);

            first = end;
        }
    }

    private void DrawSurface(SKCanvas canvas, int first, int end)
    {
        var ascending = _spikes[first].Left <= _spikes[end - 1].Left;

        var leftmost = ascending ? _spikes[first] : _spikes[end - 1];

        var rightmost = ascending ? _spikes[end - 1] : _spikes[first];

        var top = leftmost.Top;

        _surfacePoints.Clear();

        AddSurfacePoint(leftmost.Left, leftmost.Extrusion, top);

        for (var item = 0; item < end - first; item++)
        {
            var run = _spikes[ascending ? first + item : end - 1 - item];

            var ease = Math.Min(SurfaceEase, (run.Right - run.Left) / 2);

            AddSurfacePoint(run.Left + ease, run.Extrusion, top);

            if (run.Right - ease > run.Left + ease)
            {
                AddSurfacePoint(run.Right - ease, run.Extrusion, top);
            }
        }

        AddSurfacePoint(rightmost.Right, rightmost.Extrusion, top);

        var height = leftmost.Height;

        var edge = _directionY > 0 ? top : top + height;

        var offset = edge - top;

        var last = SurfacePoint(0, offset, reversed: true);

        _pathBuilder.MoveTo(leftmost.Left, edge);

        _pathBuilder.LineTo(rightmost.Right, edge);

        _pathBuilder.LineTo(last.X, last.Y);

        CurveThrough(offset, reversed: true);

        _pathBuilder.Close();

        using (var face = _pathBuilder.Detach())
        {
            _paints.Face.Color = Shade(leftmost.Colour, _directionY > 0 ? CapShade : UnderShade).WithAlpha(LandscapeAlpha);

            canvas.DrawPath(face, _paints.Face);
        }

        var side = _directionX >= 0 ? leftmost : rightmost;

        FillParallelogram(canvas,
                          Shade(leftmost.Colour, SideShade).WithAlpha(LandscapeAlpha),
                          _directionX >= 0 ? leftmost.Left : rightmost.Right,
                          top,
                          0,
                          height,
                          _directionX * side.Extrusion,
                          _directionY * side.Extrusion);

        var start = SurfacePoint(0, 0, reversed: false);

        var turn = SurfacePoint(0, height, reversed: true);

        _pathBuilder.MoveTo(start.X, start.Y);

        CurveThrough(0, reversed: false);

        _pathBuilder.LineTo(turn.X, turn.Y);

        CurveThrough(height, reversed: true);

        _pathBuilder.Close();

        using (var ribbon = _pathBuilder.Detach())
        {
            _paints.Face.Color = leftmost.Colour.WithAlpha(LandscapeAlpha);

            canvas.DrawPath(ribbon, _paints.Face);

            canvas.DrawPath(ribbon, _paints.Edge);
        }

        for (var index = first; index < end; index++)
        {
            if (_spikes[index].Named)
            {
                DrawFaceLabel(canvas, _spikes[index]);

                return;
            }
        }

        DrawCentreLabel(canvas, first, end, leftmost.Left, rightmost.Right);
    }

    private void DrawCentreLabel(SKCanvas canvas, int first, int end, float left, float right)
    {
        if (right - left < MinimumLabelWidth)
        {
            return;
        }

        var centre = (left + right) / 2;

        var run = _spikes[first];

        for (var index = first; index < end; index++)
        {
            if (_spikes[index].Left <= centre && centre < _spikes[index].Right)
            {
                run = _spikes[index];

                break;
            }
        }

        var dx = _directionX * run.Extrusion;

        var dy = _directionY * run.Extrusion;

        if (run.Source.IsOperator)
        {
            DrawOperatorLabel(canvas,
                              _operatorRows[run.Source.OperatorRow],
                              new SKRect(left + dx, run.Top + dy, right + dx, run.Top + dy + run.Height));

            return;
        }

        if (_rowHeight < MinimumLabelRowHeight)
        {
            return;
        }

        var text = TextOf(run);

        var textWidth = _paints.Font.MeasureText(text);

        _paints.Text.Color = IsLight(run.Colour) ? SKColors.Black : SKColors.White;

        canvas.Save();

        canvas.ClipRect(new SKRect(left + dx, run.Top + dy, right + dx, run.Top + dy + run.Height));

        canvas.DrawText(text,
                        Math.Max(centre + dx - textWidth / 2, left + dx + LabelPadding),
                        Baseline(run.Top + dy, run.Height),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Text);

        canvas.Restore();
    }

    private void AddSurfacePoint(float x, float extrusion, float top)
        => _surfacePoints.Add(new SKPoint(x + extrusion * _directionX, top + extrusion * _directionY));

    private SKPoint SurfacePoint(int step, float offset, bool reversed)
    {
        var point = _surfacePoints[reversed ? _surfacePoints.Count - 1 - step : step];

        return new SKPoint(point.X, point.Y + offset);
    }

    private void CurveThrough(float offset, bool reversed)
    {
        var count = _surfacePoints.Count;

        for (var step = 1; step < count - 1; step++)
        {
            var point = SurfacePoint(step, offset, reversed);

            var next = SurfacePoint(step + 1, offset, reversed);

            _pathBuilder.QuadTo(point.X, point.Y, (point.X + next.X) / 2, (point.Y + next.Y) / 2);
        }

        var final = SurfacePoint(count - 1, offset, reversed);

        _pathBuilder.LineTo(final.X, final.Y);
    }

    private PopoutBlock Raised(FlameHit source,
                               int node,
                               float left,
                               float right,
                               float top,
                               float height,
                               ulong bytes,
                               ulong maximum,
                               bool named)
    {
        var ratio = (float)Math.Min(Math.Log(1d + bytes) / Math.Log(1d + maximum), 1d);

        return new PopoutBlock(node,
                               ColourOf(node),
                               left,
                               right,
                               left,
                               right,
                               top,
                               ratio,
                               ExtrusionOf(ratio, _extrusionLength),
                               bytes,
                               named,
                               source,
                               height);
    }

    private void DrawPlayheadHandle(SKCanvas canvas, float x, double value, int width)
    {
        canvas.Save();

        canvas.Translate(x, RulerTop);

        canvas.DrawPath(_playheadTriangle, _paints.PlayheadFill);

        canvas.Restore();

        var text = PlayheadLabel(value);

        var badgeWidth = _paints.Font.MeasureText(text) + PlayheadBadgePadding * 2;

        var badgeHeight = RulerHeight - 2;

        var left = Math.Clamp(x - badgeWidth / 2, 0, Math.Max(0, width - badgeWidth));

        canvas.DrawRoundRect(new SKRect(left, RulerTop, left + badgeWidth, RulerTop + badgeHeight), 2, 2, _paints.PlayheadFill);

        canvas.DrawText(text,
                        left + PlayheadBadgePadding,
                        Baseline(RulerTop, badgeHeight),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.PlayheadText);
    }

    private string PlayheadLabel(double value)
    {
        if (_axis != TimeTravelTimelineAxis.Position || _timeline is not { } timeline)
        {
            return $"{value:N0}";
        }

        var sequence = Math.Floor(value);

        return $"{sequence:N0}:{Math.Round((value - sequence) / timeline.StepOf(_axis)):N0}";
    }

    private void Orient(int width, float bottom)
    {
        var directionX = MathF.Cos(_extrusionAngle);

        var directionY = MathF.Sin(_extrusionAngle);

        var length = MaximumLength(_popoutBlocks, directionX, directionY, width, bottom, popout: true);

        if (!_isStretching && !ShowMemory && length < _extrusionLength)
        {
            var mirrored = MaximumLength(_popoutBlocks, -directionX, directionY, width, bottom, popout: true);

            if (mirrored > length)
            {
                directionX = -directionX;

                length = mirrored;
            }
        }

        _directionX = directionX;

        _directionY = directionY;

        _drawnLength = Math.Min(_extrusionLength, length);

        for (var index = 0; index < _popoutBlocks.Count; index++)
        {
            var block = _popoutBlocks[index];

            var (left, right) = EdgesOf(block, directionX);

            _popoutBlocks[index] = block with { Left = left, Right = right, Extrusion = ExtrusionOf(block.Ratio, _drawnLength) };
        }
    }

    private float MaximumLength(List<PopoutBlock> blocks, float directionX, float directionY, int width, float bottom, bool popout)
    {
        var maximum = float.MaxValue;

        foreach (var block in blocks)
        {
            var share = ShareOf(block.Ratio);

            if (share <= 0)
            {
                continue;
            }

            var room = float.MaxValue;

            var (left, right) = popout ? EdgesOf(block, directionX) : (block.Left, block.Right);

            if (directionX != 0 && (popout || (directionX > 0 ? ShowsEnd && right <= width : ShowsStart && left >= 0)))
            {
                var label = popout ? LabelWidth(block) : 0;

                room = directionX > 0 ? (width - right - label) / directionX : (left - label) / -directionX;
            }

            if (directionY < 0)
            {
                room = Math.Min(room, block.Top / -directionY);
            }
            else if (directionY > 0)
            {
                room = Math.Min(room, (bottom - block.Top - block.Height) / directionY);
            }

            maximum = Math.Min(maximum, MinimumExtrusion + (room - MinimumExtrusion) / share);
        }

        return Math.Max(MinimumExtrusion, maximum);
    }

    private float LabelWidth(PopoutBlock block)
        => ShowMemory && block.Label > 0 && _rowHeight >= MinimumLabelRowHeight
            ? _paints.Font.MeasureText(SizeFormat.Format((long)block.Label)) + LabelPadding * 3
            : 0;

    private int DrawIndex(int index) => _directionY > 0 ? _popoutBlocks.Count - 1 - index : index;

    private (float Left, float Right) EdgesOf(PopoutBlock block, float directionX)
    {
        if (directionX >= 0)
        {
            var left = Math.Max(block.SpanLeft, _popoutX);

            return (left, Math.Max(Math.Min(block.SpanRight, _popoutX + SliceWidth), left + NarrowSpan));
        }

        var right = Math.Min(block.SpanRight, _popoutX);

        return (Math.Min(Math.Max(block.SpanLeft, _popoutX - SliceWidth), right - NarrowSpan), right);
    }

    private static SKPath PlayheadTriangle()
    {
        using var builder = new SKPathBuilder();

        builder.MoveTo(0, RulerHeight + PlayheadStripHeight);

        builder.LineTo(-PlayheadHalfWidth, RulerHeight);

        builder.LineTo(PlayheadHalfWidth, RulerHeight);

        builder.Close();

        return builder.Detach();
    }

    private void DrawMemoryBand(SKCanvas canvas, TimeTravelTimeline timeline, int width, int height, double scale)
    {
        var top = height - MemoryBandHeight;

        var inUseTop = height - InUseBandHeight;

        var buckets = Math.Max(1, (int)Math.Ceiling(width / MemoryBucketWidth));

        var allocated = new ulong[buckets];

        var inUse = new ulong[buckets];

        _bandPeak = 0;

        _inUsePeak = 0;

        for (var bucket = 0; bucket < buckets; bucket++)
        {
            var (start, end) = BucketRange(bucket, scale);

            allocated[bucket] = AllocatedDuring(timeline, start, end);

            inUse[bucket] = timeline.PeakInUseDuring(start, end);

            _bandPeak = Math.Max(_bandPeak, allocated[bucket]);

            _inUsePeak = Math.Max(_inUsePeak, inUse[bucket]);
        }

        var granted = (ulong)Math.Max(0, GrantedMemory);

        _inUseScale = Math.Max(_inUsePeak, granted);

        var legendRight = width - LabelPadding * 4;

        canvas.DrawText("Allocated", legendRight, Baseline(top, MemoryBandLabelHeight), SKTextAlign.Right, _paints.Font, _paints.Label);

        if (_inUsePeak > 0)
        {
            canvas.DrawText($"In Use, Peak {SizeFormat.Format((long)_inUsePeak)}",
                            legendRight,
                            Baseline(inUseTop, MemoryBandLabelHeight),
                            SKTextAlign.Right,
                            _paints.Font,
                            _paints.InUseText);
        }

        if (_bandPeak > 0)
        {
            DrawBandLine(canvas, allocated, _bandPeak, inUseTop - 1f, AllocatedBandHeight, _paints.AreaLine);
        }

        if (granted > 0)
        {
            DrawGrant(canvas, granted, width, height);
        }

        if (_inUsePeak > 0)
        {
            DrawBandLine(canvas, inUse, _inUseScale, height - 1f, InUseBandHeight, _paints.InUseLine);
        }
    }

    private void DrawGrant(SKCanvas canvas, ulong granted, int width, int height)
    {
        var top = BandY(granted, _inUseScale, height - 1f, InUseBandHeight);

        canvas.DrawRect(0, top, width, height - top, _paints.GrantFill);

        canvas.DrawLine(0, top, width, top, _paints.GrantEdge);

        canvas.DrawText($"Granted {SizeFormat.Format((long)granted)}",
                        LabelPadding * 2,
                        top - GrantLabelGap,
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Label);
    }

    private void DrawBandLine(SKCanvas canvas, ulong[] values, ulong peak, float bottom, float rowHeight, SKPaint paint)
    {
        _pathBuilder.MoveTo(0.5f * MemoryBucketWidth, BandY(values[0], peak, bottom, rowHeight));

        for (var bucket = 1; bucket < values.Length; bucket++)
        {
            _pathBuilder.LineTo((bucket + 0.5f) * MemoryBucketWidth, BandY(values[bucket], peak, bottom, rowHeight));
        }

        using var line = _pathBuilder.Detach();

        canvas.DrawPath(line, paint);
    }

    private void DrawBandMarker(SKCanvas canvas, TimeTravelTimeline timeline, float x, int width, int height, double scale)
    {
        if (!ShowsMemoryBand || x < 0 || x >= width)
        {
            return;
        }

        var (start, end) = BucketRange((int)(x / MemoryBucketWidth), scale);

        var inUseTop = height - InUseBandHeight;

        if (_bandPeak > 0)
        {
            var allocated = AllocatedDuring(timeline, start, end);

            DrawMarker(canvas,
                       x,
                       BandY(allocated, _bandPeak, inUseTop - 1f, AllocatedBandHeight),
                       $"Allocated {SizeFormat.Format((long)allocated)}",
                       _paints.AreaLine.Color,
                       height - MemoryBandHeight,
                       inUseTop,
                       width);
        }

        if (_inUsePeak > 0)
        {
            var inUse = timeline.PeakInUseDuring(start, end);

            DrawMarker(canvas,
                       x,
                       BandY(inUse, _inUseScale, height - 1f, InUseBandHeight),
                       $"In Use {SizeFormat.Format((long)inUse)}",
                       _paints.InUseLine.Color,
                       inUseTop,
                       height,
                       width);
        }
    }

    private void DrawMarker(SKCanvas canvas, float x, float y, string text, SKColor colour, float top, float bottom, int width)
    {
        _paints.Face.Color = colour;

        canvas.DrawCircle(x, y, MarkerRadius, _paints.Face);

        var textWidth = _paints.Font.MeasureText(text);

        var left = x + MarkerRadius + LabelPadding + textWidth + LabelPadding * 2 > width
            ? x - MarkerRadius - LabelPadding - textWidth - LabelPadding * 2
            : x + MarkerRadius + LabelPadding;

        var labelTop = Math.Clamp(y - MemoryBandLabelHeight / 2, top + 1, bottom - MemoryBandLabelHeight - 1);

        canvas.DrawRect(new SKRect(left, labelTop, left + textWidth + LabelPadding * 2, labelTop + MemoryBandLabelHeight),
                        _paints.LabelBackground);

        canvas.DrawText(text,
                        left + LabelPadding,
                        Baseline(labelTop, MemoryBandLabelHeight),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Label);
    }

    private static ulong AllocatedDuring(TimeTravelTimeline timeline, double start, double end)
        => timeline.Threads.Aggregate(0ul, (total, t) => total + timeline.AllocatedDuring(t.ThreadId, start, end).Bytes);

    private (double Start, double End) BucketRange(int bucket, double scale)
    {
        var start = _viewStart + bucket * MemoryBucketWidth / scale;

        return (start, start + MemoryBucketWidth / scale);
    }

    private static float BandY(ulong bytes, ulong peak, float bottom, float rowHeight)
    {
        var chartHeight = rowHeight - MemoryBandLabelHeight - 2f;

        return peak == 0 ? bottom : bottom - chartHeight * (float)((double)bytes / peak);
    }

    private void DrawBlock(SKCanvas canvas, PopoutBlock block, byte alpha = byte.MaxValue, bool named = true)
    {
        var bottom = block.Top + block.Height;

        var dx = _directionX * block.Extrusion;

        var dy = _directionY * block.Extrusion;

        if (block.Extrusion > 0)
        {
            FillParallelogram(canvas,
                              Shade(block.Colour, dy > 0 ? CapShade : UnderShade).WithAlpha(alpha),
                              block.Left,
                              dy > 0 ? block.Top : bottom,
                              block.Right - block.Left,
                              0,
                              dx,
                              dy);

            var side = Shade(block.Colour, SideShade).WithAlpha(alpha);

            FillParallelogram(canvas, side, dx < 0 ? block.Right : block.Left, block.Top, 0, block.Height, dx, dy);

            if (named)
            {
                DrawSideText(canvas, block, side, dx, dy);
            }
        }

        var front = FrontOf(block);

        _paints.Face.Color = block.Colour.WithAlpha(alpha);

        canvas.DrawRect(front, _paints.Face);

        canvas.DrawRect(front, _paints.Edge);
    }

    private void DrawSideText(SKCanvas canvas, PopoutBlock block, SKColor face, float dx, float dy)
    {
        var length = MathF.Sqrt(dx * dx + dy * dy);

        if (length < MinimumLabelWidth || _rowHeight < MinimumLabelRowHeight)
        {
            return;
        }

        var forward = dx >= 0;

        var edge = dx < 0 ? block.Right : block.Left;

        var matrix = new SKMatrix((forward ? dx : -dx) / length,
                                  0,
                                  forward ? edge : edge + dx,
                                  (forward ? dy : -dy) / length,
                                  1,
                                  forward ? block.Top : block.Top + dy,
                                  0,
                                  0,
                                  1);

        _paints.Text.Color = IsLight(face) ? SKColors.Black : SKColors.White;

        canvas.Save();

        canvas.Concat(in matrix);

        canvas.ClipRect(new SKRect(0, 0, length, block.Height));

        canvas.DrawText(TextOf(block), LabelPadding, Baseline(0, block.Height), SKTextAlign.Left, _paints.Font, _paints.Text);

        canvas.Restore();
    }

    private void DrawBlockLabel(SKCanvas canvas, PopoutBlock block)
    {
        var front = FrontOf(block);

        var text = SizeFormat.Format((long)block.Label);

        var outward = _directionX >= 0 ? 1f : -1f;

        var edge = _directionX >= 0 ? front.Right : front.Left;

        canvas.DrawRect(LabelRectOf(block, _paints.Font.MeasureText(text)), _paints.LabelBackground);

        canvas.DrawText(text,
                        edge + outward * LabelPadding * 2,
                        Baseline(front.Top, front.Height),
                        _directionX >= 0 ? SKTextAlign.Left : SKTextAlign.Right,
                        _paints.Font,
                        _paints.Label);
    }

    private SKRect LabelRectOf(PopoutBlock block, float textWidth)
    {
        var front = FrontOf(block);

        var outward = _directionX >= 0 ? 1f : -1f;

        var edge = _directionX >= 0 ? front.Right : front.Left;

        var near = edge + outward * LabelPadding;

        var far = edge + outward * (textWidth + LabelPadding * 3);

        return new SKRect(Math.Min(near, far), front.Top, Math.Max(near, far), front.Bottom);
    }

    private SKRect FrontOf(PopoutBlock block)
        => new(block.Left + _directionX * block.Extrusion,
               block.Top + _directionY * block.Extrusion,
               block.Right + _directionX * block.Extrusion,
               block.Top + block.Height + _directionY * block.Extrusion);

    private void FillParallelogram(SKCanvas canvas, SKColor colour, float x, float y, float ax, float ay, float bx, float by)
    {
        var matrix = new SKMatrix(ax, bx, x, ay, by, y, 0, 0, 1);

        _paints.Face.Color = colour;

        canvas.Save();

        canvas.Concat(in matrix);

        canvas.DrawRect(0, 0, 1, 1, _paints.Face);

        canvas.Restore();
    }

    private float RatioOf(ulong bytes)
    {
        if (bytes == 0 || _memoryMaximum == 0)
        {
            return 0;
        }

        return (float)Math.Min(Math.Log(1d + bytes) / Math.Log(1d + _memoryMaximum), 1d);
    }

    private float ExtrusionOf(float ratio, float length)
        => ShareOf(ratio) is > 0 and var share ? MinimumExtrusion + (length - MinimumExtrusion) * share : 0;

    private float ShareOf(float ratio) => ShowMemory ? ratio : BaseShare + (1 - BaseShare) * ratio;

    private static SKColor Shade(SKColor colour, float factor)
        => new((byte)(colour.Red * factor), (byte)(colour.Green * factor), (byte)(colour.Blue * factor), colour.Alpha);

    private SKRect? SpanRect(FlameHit hit, int width)
    {
        if (_visible is not { } timeline || hit.Lane >= timeline.Threads.Count)
        {
            return null;
        }

        var thread = timeline.Threads[hit.Lane];

        if (hit.Depth >= DepthOf(thread))
        {
            return null;
        }

        var row = thread.Rows[hit.Depth];

        if (hit.Index >= row.Count)
        {
            return null;
        }

        var scale = PixelsPerUnit(width);

        var left = (float)((row.Starts(_axis)[hit.Index] - _viewStart) * scale);

        var right = (float)((row.Ends(_axis)[hit.Index] - _viewStart) * scale);

        var top = ContentTop + _laneTops[hit.Lane] - (float)_scrollY + LaneHeaderHeight + hit.Depth * _rowHeight;

        return new SKRect(Math.Max(left, -2f), top, Math.Min(Math.Max(right, left + NarrowSpan), width + 2f), top + BarHeight);
    }

    private float Baseline(float top, float height)
    {
        var metrics = _paints.Font.Metrics;

        return top + (height - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent;
    }

    private SKColor ColourOf(int node)
    {
        if (_colours.TryGetValue(node, out var colour))
        {
            return colour;
        }

        colour = _timeline?.NodeOf(node)?.CategoryColour is { } hex ? Parse(hex) : _paints.Unknown;

        _colours[node] = colour;

        return colour;
    }

    private SKColor Parse(string hex)
    {
        if (!_parsedColours.TryGetValue(hex, out var colour))
        {
            colour = SKColor.TryParse(hex, out var parsed) ? parsed : _paints.Unknown;

            _parsedColours[hex] = colour;
        }

        return colour;
    }

    private string LabelOf(int node)
    {
        if (!_labels.TryGetValue(node, out var label))
        {
            label = _timeline?.NodeOf(node)?.Symbol is { Length: > 0 } symbol ? symbol : "Unknown";

            _labels[node] = label;
        }

        return label;
    }

    private static bool IsLight(SKColor colour) => colour.Red * 0.299 + colour.Green * 0.587 + colour.Blue * 0.114 > 160;

    private static double TickStep(double minimum)
    {
        if (minimum <= 0 || double.IsNaN(minimum) || double.IsInfinity(minimum))
        {
            return 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(minimum)));

        foreach (var factor in (ReadOnlySpan<double>)[1, 2, 5, 10])
        {
            if (magnitude * factor >= minimum)
            {
                return magnitude * factor;
            }
        }

        return magnitude * 10;
    }

    private string FormatAxis(double value, double step)
    {
        if (_axis == TimeTravelTimelineAxis.Position)
        {
            return step >= 1 ? $"{value:N0}" : $"{value:N3}";
        }

        return value switch
        {
            >= 1_000_000_000 => $"{value / 1_000_000_000:0.##}B",
            >= 1_000_000 => $"{value / 1_000_000:0.##}M",
            >= 1_000 => $"{value / 1_000:0.##}K",
            _ => $"{value:N0}"
        };
    }

    private readonly record struct PopoutBlock(int Node,
                                               SKColor Colour,
                                               float SpanLeft,
                                               float SpanRight,
                                               float Left,
                                               float Right,
                                               float Top,
                                               float Ratio,
                                               float Extrusion,
                                               ulong Label,
                                               bool Named,
                                               FlameHit Source,
                                               float Height);

    private readonly record struct LayerKey(int Width,
                                            int Height,
                                            double ViewStart,
                                            double ViewEnd,
                                            double ScrollY,
                                            int Version);

    private readonly record struct LandscapeKey(LayerKey Layer, float Angle, float Length);

    private sealed record RaisedRow(int[] Indexes, ulong[] Bytes);
}
