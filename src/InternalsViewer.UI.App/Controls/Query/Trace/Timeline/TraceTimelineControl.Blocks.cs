using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.UI.App.Controls.Query.Timeline;
using InternalsViewer.UI.App.Helpers;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline;

public sealed partial class TraceTimelineControl
{
    private const float MinimumExtrusion = 3f;

    private const float DefaultExtrusionLength = 140f;

    private const float DefaultExtrusionAngle = -0.2738f;

    private const float BaseShare = 0.25f;

    private const float CapShade = 0.85f;

    private const float UnderShade = 0.55f;

    private const float SideShade = 0.68f;

    private const byte LandscapeAlpha = 200;

    private float _extrusionAngle = DefaultExtrusionAngle;

    private float _extrusionLength = DefaultExtrusionLength;

    private float _drawnLength = DefaultExtrusionLength;

    private float _directionX = MathF.Cos(DefaultExtrusionAngle);

    private float _directionY = MathF.Sin(DefaultExtrusionAngle);

    private Block Raised(FlameHit call, int callNodeIndex, float left, float right, float top, float height, ulong bytes, ulong maximum)
    {
        var ratio = LogRatio(bytes, maximum);

        return new Block(call, callNodeIndex, ColourOf(callNodeIndex), left, right, top, height, ratio, ExtrusionOf(ratio, _extrusionLength), bytes);
    }

    private static float LogRatio(ulong bytes, ulong maximum)
        => bytes == 0 || maximum == 0 ? 0 : (float)Math.Min(Math.Log(1d + bytes) / Math.Log(1d + maximum), 1d);

    private float MaximumLength(IEnumerable<(Block Block, float Left, float Right)> blocks,
                                float directionX,
                                float directionY,
                                int width,
                                float bottom)
    {
        var maximum = float.MaxValue;

        foreach (var (block, left, right) in blocks)
        {
            var share = ShareOf(block.Ratio);

            if (share <= 0)
            {
                continue;
            }

            var room = float.MaxValue;

            if (directionX != 0 && (directionX > 0 ? ShowsEnd && right <= width : ShowsStart && left >= 0))
            {
                room = directionX > 0 ? (width - right) / directionX : left / -directionX;
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

    private static IEnumerable<(Block Block, float Left, float Right)> Flat(List<Block> blocks) => blocks.Select(b => (b, b.Left, b.Right));

    private void DrawBlock(SKCanvas canvas, Block block, byte alpha = byte.MaxValue, bool sideText = true)
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

            if (sideText)
            {
                DrawSideText(canvas, block, side, dx, dy);
            }
        }

        var front = FrontOf(block);

        _paints.Face.Color = block.Colour.WithAlpha(alpha);

        canvas.DrawRect(front, _paints.Face);

        canvas.DrawRect(front, _paints.Edge);
    }

    private void DrawSideText(SKCanvas canvas, Block block, SKColor face, float dx, float dy)
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

        _paints.Text.Color = TimelineColours.IsLight(face) ? SKColors.Black : SKColors.White;

        canvas.Save();

        canvas.Concat(in matrix);

        canvas.ClipRect(new SKRect(0, 0, length, block.Height));

        canvas.DrawText(LabelOf(block.CallNodeIndex), LabelPadding, Baseline(0, block.Height), SKTextAlign.Left, _paints.TiltedFont, _paints.Text);

        canvas.Restore();
    }

    private void DrawBlockLabel(SKCanvas canvas, Block block)
    {
        var front = FrontOf(block);

        var text = SizeFormat.Format((long)block.Bytes);

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

    private SKRect LabelRectOf(Block block, float textWidth)
    {
        var front = FrontOf(block);

        var outward = _directionX >= 0 ? 1f : -1f;

        var edge = _directionX >= 0 ? front.Right : front.Left;

        var near = edge + outward * LabelPadding;

        var far = edge + outward * (textWidth + LabelPadding * 3);

        return new SKRect(Math.Min(near, far), front.Top, Math.Max(near, far), front.Bottom);
    }

    private SKRect FrontOf(Block block)
        => new(block.Left + _directionX * block.Extrusion,
               block.Top + _directionY * block.Extrusion,
               block.Right + _directionX * block.Extrusion,
               block.Top + block.Height + _directionY * block.Extrusion);

    private void DrawBlockOutline(SKCanvas canvas, Block block, SKPaint paint)
    {
        if (block.Extrusion <= 0)
        {
            StrokeUnion(canvas, paint, BuildRect(FrontOf(block)));

            return;
        }

        var bottom = block.Top + block.Height;

        var dx = _directionX * block.Extrusion;

        var dy = _directionY * block.Extrusion;

        StrokeUnion(canvas,
                    paint,
                    BuildRect(new SKRect(block.Left, block.Top, block.Right, bottom)),
                    BuildParallelogram(block.Left, dy > 0 ? block.Top : bottom, block.Right - block.Left, 0, dx, dy),
                    BuildParallelogram(dx < 0 ? block.Right : block.Left, block.Top, 0, block.Height, dx, dy),
                    BuildRect(FrontOf(block)));
    }

    private SKPath BuildRect(SKRect rect)
    {
        _pathBuilder.AddRect(rect);

        return _pathBuilder.Detach();
    }

    private SKPath BuildParallelogram(float x, float y, float ax, float ay, float bx, float by)
    {
        _pathBuilder.MoveTo(x, y);

        _pathBuilder.LineTo(x + ax, y + ay);

        _pathBuilder.LineTo(x + ax + bx, y + ay + by);

        _pathBuilder.LineTo(x + bx, y + by);

        _pathBuilder.Close();

        return _pathBuilder.Detach();
    }

    private static void StrokeUnion(SKCanvas canvas, SKPaint paint, params SKPath[] pieces)
    {
        SKPath? outline = pieces[0];

        for (var index = 1; index < pieces.Length && outline is not null; index++)
        {
            var merged = outline.Op(pieces[index], SKPathOp.Union);

            if (!ReferenceEquals(outline, pieces[0]))
            {
                outline.Dispose();
            }

            outline = merged;
        }

        if (outline is null)
        {
            foreach (var piece in pieces)
            {
                canvas.DrawPath(piece, paint);
            }
        }
        else
        {
            canvas.DrawPath(outline, paint);

            if (!ReferenceEquals(outline, pieces[0]))
            {
                outline.Dispose();
            }
        }

        foreach (var piece in pieces)
        {
            piece.Dispose();
        }
    }

    private void FillParallelogram(SKCanvas canvas, SKColor colour, float x, float y, float ax, float ay, float bx, float by)
    {
        var matrix = new SKMatrix(ax, bx, x, ay, by, y, 0, 0, 1);

        _paints.Face.Color = colour;

        canvas.Save();

        canvas.Concat(in matrix);

        canvas.DrawRect(0, 0, 1, 1, _paints.Face);

        canvas.Restore();
    }

    private float ExtrusionOf(float ratio, float length)
        => ShareOf(ratio) is > 0 and var share ? MinimumExtrusion + (length - MinimumExtrusion) * share : 0;

    private float ShareOf(float ratio) => ShowMemory ? ratio : BaseShare + (1 - BaseShare) * ratio;

    private static SKColor Shade(SKColor colour, float factor)
        => new((byte)(colour.Red * factor), (byte)(colour.Green * factor), (byte)(colour.Blue * factor), colour.Alpha);

    private readonly record struct Block(FlameHit Call,
                                         int CallNodeIndex,
                                         SKColor Colour,
                                         float Left,
                                         float Right,
                                         float Top,
                                         float Height,
                                         float Ratio,
                                         float Extrusion,
                                         ulong Bytes);
}
