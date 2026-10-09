using System;
using System.Collections.Generic;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float SpikeMinimumWidth = 3f;

    private const float SurfaceBucketWidth = 3f;

    private const float SurfaceEase = 6f;

    private readonly List<Block> _spikes = [];

    private readonly List<int> _spikeRows = [];

    private readonly List<Block> _reversedSpikes = [];

    private readonly Dictionary<FlameHit, int> _spikeIndex = [];

    private readonly List<SKPoint> _surfacePoints = [];

    private SKPicture? _landscapeLayer;

    private LandscapeKey _landscapeLayerKey;

    private SKPicture RecordLandscape(int width, int height)
    {
        using var recorder = new SKPictureRecorder();

        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));

        _spikes.Clear();

        _spikeRows.Clear();

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

        if (_directionY > 0)
        {
            ReverseRows();
        }

        var length = Math.Min(_extrusionLength, MaximumLength(Flat(_spikes), _directionX, _directionY, width, bottom, popout: false));

        for (var index = 0; index < _spikes.Count; index++)
        {
            _spikes[index] = _spikes[index] with { Extrusion = ExtrusionOf(_spikes[index].Ratio, length) };

            if (_spikes[index].Source.Call is { } call)
            {
                _spikeIndex[call] = index;
            }
        }

        if (MemoryMode == FlameChartMemoryMode.InUse)
        {
            DrawSurfaces(canvas);

            return;
        }

        foreach (var spike in _spikes)
        {
            DrawBlock(canvas, spike, LandscapeAlpha, sideText: false);

            DrawFaceLabel(canvas, spike);
        }
    }

    private void ReverseRows()
    {
        _reversedSpikes.Clear();

        for (var row = _spikeRows.Count - 1; row >= 0; row--)
        {
            var end = row + 1 < _spikeRows.Count ? _spikeRows[row + 1] : _spikes.Count;

            for (var index = _spikeRows[row]; index < end; index++)
            {
                _reversedSpikes.Add(_spikes[index]);
            }
        }

        _spikes.Clear();

        _spikes.AddRange(_reversedSpikes);
    }

    private void DrawFaceLabel(SKCanvas canvas, Block block)
    {
        var front = FrontOf(block);

        if (block.Source.Operator is { } operatorHit && operatorHit.Row < _operatorRows.Length)
        {
            DrawOperatorLabel(canvas, _operatorRows[operatorHit.Row], front);

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

    private void DrawSpikeHighlight(SKCanvas canvas, FlameHit? hit, SKPaint paint)
    {
        if (hit is { } highlighted && SpikeOf(highlighted) is { } spike)
        {
            DrawWireframe(canvas, spike, paint);
        }
    }

    private Block? SpikeOf(FlameHit hit) => _spikeIndex.TryGetValue(hit, out var index) ? _spikes[index] : null;

    private void CollectSpikes(TimeTravelTimeline timeline, float bottom, double scale)
    {
        _spikes.Clear();

        _spikeRows.Clear();

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

                _spikeRows.Add(rowStart);

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

                    var spike = Raised(BlockSource.Of(new FlameHit(lane, depth, index)),
                                       row.NodeAt(index),
                                       left,
                                       right,
                                       top,
                                       BarHeight,
                                       raised.Bytes[item],
                                       _selfMaximum);

                    var last = _spikes.Count - 1;

                    if (last >= rowStart && left < _spikes[last].Right)
                    {
                        var dominant = spike.Bytes > _spikes[last].Bytes ? spike : _spikes[last];

                        _spikes[last] = Raised(dominant.Source,
                                               dominant.Node,
                                               _spikes[last].Left,
                                               Math.Max(_spikes[last].Right, right),
                                               top,
                                               BarHeight,
                                               _spikes[last].Bytes + spike.Bytes,
                                               _selfMaximum);
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

        _spikeRows.Clear();

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

                _spikeRows.Add(rowStart);

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

        var blockSource = BlockSource.Of(source);

        var first = _spikes.Count;

        for (var x = left; x < right; x += SurfaceBucketWidth)
        {
            var end = Math.Min(x + SurfaceBucketWidth, right);

            var bytes = curve.PeakDuring(PositionAt(row, source.Index, _viewStart + x / scale),
                                         PositionAt(row, source.Index, _viewStart + end / scale));

            var last = _spikes.Count - 1;

            if (last >= first && _spikes[last].Bytes == bytes && _spikes[last].Right >= x)
            {
                _spikes[last] = _spikes[last] with { Right = end };
            }
            else
            {
                _spikes.Add(Raised(blockSource, node, x, end, top, BarHeight, bytes, _inUseMaximum));
            }
        }

        FinishSurface(first);
    }

    private void FinishSurface(int first)
    {
        for (var index = first; index < _spikes.Count; index++)
        {
            if (_spikes[index].Bytes > 0)
            {
                return;
            }
        }

        _spikes.RemoveRange(first, _spikes.Count - first);
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

        if (NamedRun(first, end) is >= 0 and var named)
        {
            DrawFaceLabel(canvas, _spikes[named]);

            return;
        }

        DrawCentreLabel(canvas, first, end, leftmost.Left, rightmost.Right);
    }

    private int NamedRun(int first, int end)
    {
        var needed = _spikes[first].Source.Operator is not null
            ? MinimumOperatorLabelWidth + LabelPadding * 4
            : _paints.Font.MeasureText(TextOf(_spikes[first])) + LabelPadding * 2;

        var widest = -1;

        for (var index = first; index < end; index++)
        {
            var slab = _spikes[index].Right - _spikes[index].Left;

            if (_spikes[index].Bytes > 0 && slab >= needed && (widest < 0 || slab > _spikes[widest].Right - _spikes[widest].Left))
            {
                widest = index;
            }
        }

        return widest;
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

        if (run.Source.Operator is { } operatorHit && operatorHit.Row < _operatorRows.Length)
        {
            DrawOperatorLabel(canvas,
                              _operatorRows[operatorHit.Row],
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

    private readonly record struct LandscapeKey(LayerKey Layer, float Angle, float Length);
}
