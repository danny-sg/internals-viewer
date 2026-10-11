using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float PlayheadHalfWidth = 9f;

    private const float PlayheadBadgePadding = 4f;

    private const float SliceWidth = 16f;

    private readonly List<Popout> _popouts = [];

    private float _popoutX;

    private void DrawPopout(SKCanvas canvas, int width, int height)
    {
        _popouts.Clear();

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

                var ratio = LogRatio(bytes, _memoryMaximum);

                var inheriting = bytes == 0 && inherited > 0;

                inherited = inheriting ? inherited : ratio;

                if (top + _rowHeight < ContentTop)
                {
                    continue;
                }

                var block = new Block(BlockSource.Of(new FlameHit(lane, depth, index)),
                                      node,
                                      ColourOf(node),
                                      x,
                                      x,
                                      top,
                                      BarHeight,
                                      inherited,
                                      0,
                                      bytes);

                _popouts.Add(new Popout(block, spanLeft, spanRight));
            }
        }

        Orient(width, height - BandHeight);

        for (var index = 0; index < _popouts.Count; index++)
        {
            DrawBlock(canvas, _popouts[DrawIndex(index)].Block);
        }

        canvas.Restore();

        DrawPlayheadHandle(canvas, x, value, width);
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

        var length = MaximumLength(Edged(directionX), directionX, directionY, width, bottom, popout: true);

        if (!_isStretching && !ShowMemory && length < _extrusionLength)
        {
            var mirrored = MaximumLength(Edged(-directionX), -directionX, directionY, width, bottom, popout: true);

            if (mirrored > length)
            {
                directionX = -directionX;

                length = mirrored;
            }
        }

        _directionX = directionX;

        _directionY = directionY;

        _drawnLength = Math.Min(_extrusionLength, length);

        for (var index = 0; index < _popouts.Count; index++)
        {
            var popout = _popouts[index];

            var (left, right) = EdgesOf(popout, directionX);

            var block = popout.Block with { Left = left, Right = right, Extrusion = ExtrusionOf(popout.Block.Ratio, _drawnLength) };

            _popouts[index] = popout with { Block = block };
        }
    }

    private IEnumerable<(Block Block, float Left, float Right)> Edged(float directionX)
    {
        foreach (var popout in _popouts)
        {
            var (left, right) = EdgesOf(popout, directionX);

            yield return (popout.Block, left, right);
        }
    }

    private int DrawIndex(int index) => _directionY > 0 ? _popouts.Count - 1 - index : index;

    private (float Left, float Right) EdgesOf(Popout popout, float directionX)
    {
        if (directionX >= 0)
        {
            var left = Math.Max(popout.SpanLeft, _popoutX);

            return (left, Math.Max(Math.Min(popout.SpanRight, _popoutX + SliceWidth), left + NarrowSpan));
        }

        var right = Math.Min(popout.SpanRight, _popoutX);

        return (Math.Min(Math.Max(popout.SpanLeft, _popoutX - SliceWidth), right - NarrowSpan), right);
    }

    private Block? PopoutOf(FlameHit hit)
    {
        foreach (var popout in _popouts)
        {
            if (popout.Block.Source.Call == hit && popout.Block.Extrusion > 0)
            {
                return popout.Block;
            }
        }

        return null;
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

    private readonly record struct Popout(Block Block, float SpanLeft, float SpanRight);
}
