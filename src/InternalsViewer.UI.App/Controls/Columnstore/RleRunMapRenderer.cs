using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Columnstore;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// Draws the RLE array as two tracks of runs for value runs and BitPack runs
/// </summary>
public sealed class RleRunMapRenderer : IDisposable
{
    public const float TrackHeight = 20f;

    public const float TrackGap = 4f;

    public const float MarkerHeight = 6f;

    public const float GutterWidth = 58f;

    public static float TotalHeight => (TrackHeight * 2) + TrackGap + MarkerHeight + 2;

    private const int HueWheel = 256;

    private const int LiteralSaturation = 150;

    private const int LiteralValue = 220;

    private const int BitpackSaturation = 110;

    private const int BitpackValue = 110;

    private readonly SKPaint _fill = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    private readonly SKPaint _marker = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private readonly SKPaint _stroke = new() { IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

    private readonly SKPaint _text = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private static readonly SKTypeface LabelTypeface = SKTypeface.FromFamilyName("Segoe UI Variable Text") ?? SKTypeface.Default;

    private readonly SKFont _font = new(LabelTypeface, 11f)
    {
        Edging = SKFontEdging.SubpixelAntialias,
        Subpixel = true
    };

    public SKColor TrackColour { get; set; } = ColumnstoreColours.Panel;

    public SKColor SelectionColour { get; set; } = ColumnstoreColours.Selection;

    public SKColor LabelColour { get; set; } = ColumnstoreColours.Muted;

    public int SelectedIndex { get; set; } = -1;

    /// <summary>
    /// Row the caret points at, which is where the reader clicked rather than the middle of the run holding it
    /// </summary>
    public int SelectedRow { get; set; } = -1;

    public void Draw(SKCanvas canvas, IReadOnlyList<RleRunDetail> runs, float width, int firstRow, int rowSpan)
    {
        var trackWidth = width - GutterWidth;

        if (trackWidth < 1 || rowSpan <= 0)
        {
            return;
        }

        var scale = RunScale.Build(runs);

        DrawTrack(canvas, runs, scale, trackWidth, firstRow, rowSpan, 0, "Value", false);

        DrawTrack(canvas, runs, scale, trackWidth, firstRow, rowSpan, TrackHeight + TrackGap, "Bit Pack", true);

        DrawMarker(canvas, runs, trackWidth, firstRow, rowSpan);
    }

    private void DrawTrack(SKCanvas canvas,
                           IReadOnlyList<RleRunDetail> runs,
                           RunScale scale,
                           float trackWidth,
                           int firstRow,
                           int rowSpan,
                           float top,
                           string label,
                           bool isBitpacked)
    {
        var bounds = new SKRect(GutterWidth, top, GutterWidth + trackWidth, top + TrackHeight);

        _fill.Color = TrackColour;

        canvas.DrawRect(bounds, _fill);

        _text.Color = LabelColour;

        canvas.DrawText(label,
                        GutterWidth - 6,
                        top + (TrackHeight / 2) + (_font.Size / 2) - 1,
                        SKTextAlign.Right,
                        _font,
                        _text);

        // Runs narrower than a pixel would vanish, so each pixel takes the last run to cover any of it
        var owners = new int[(int)trackWidth];

        Array.Fill(owners, -1);

        foreach (var run in runs)
        {
            if (run.Count <= 0 || run.IsBitpacked != isBitpacked)
            {
                continue;
            }

            var from = ToPixel(run.StartRow, firstRow, rowSpan, trackWidth);

            var to = (int)Math.Ceiling((run.StartRow + run.Count - firstRow) / (double)rowSpan * trackWidth);

            for (var x = Math.Max(0, from); x < Math.Min(owners.Length, Math.Max(to, from + 1)); x++)
            {
                owners[x] = run.Index;
            }
        }

        var start = 0;

        for (var x = 1; x <= owners.Length; x++)
        {
            if (x < owners.Length && owners[x] == owners[start])
            {
                continue;
            }

            if (owners[start] >= 0)
            {
                _fill.Color = scale.GetColour(runs[owners[start]]);

                canvas.DrawRect(new SKRect(GutterWidth + start, bounds.Top, GutterWidth + x, bounds.Bottom), _fill);
            }

            start = x < owners.Length ? x : start;
        }
    }

    /// <summary>
    /// A caret under the tracks pointing at where the selected run sits
    /// </summary>
    private void DrawMarker(SKCanvas canvas, IReadOnlyList<RleRunDetail> runs, float trackWidth, int firstRow, int rowSpan)
    {
        if (SelectedIndex < 0 || SelectedIndex >= runs.Count)
        {
            return;
        }

        var run = runs[SelectedIndex];

        var top = run.IsBitpacked ? TrackHeight + TrackGap : 0;

        var from = GutterWidth + ToPixel(run.StartRow, firstRow, rowSpan, trackWidth);

        var to = GutterWidth + ToPixel(run.StartRow + run.Count, firstRow, rowSpan, trackWidth);

        _stroke.Color = SelectionColour;

        canvas.DrawRect(new SKRect(from, top + 0.5f, Math.Max(from + 1, to), top + TrackHeight - 0.5f), _stroke);

        var centre = SelectedRow >= 0
            ? GutterWidth + ToPixel(SelectedRow, firstRow, rowSpan, trackWidth)
            : (from + to) / 2;

        centre = Math.Clamp(centre, GutterWidth, GutterWidth + trackWidth);

        var caretTop = (TrackHeight * 2) + TrackGap + 1;

        var builder = new SKPathBuilder();

        builder.MoveTo(centre, caretTop);
        builder.LineTo(centre - MarkerHeight, caretTop + MarkerHeight);
        builder.LineTo(centre + MarkerHeight, caretTop + MarkerHeight);
        builder.Close();

        using var path = builder.Detach();

        _marker.Color = SelectionColour;

        canvas.DrawPath(path, _marker);
    }

    private static int ToPixel(int row, int firstRow, int rowSpan, float trackWidth)
        => (int)((row - firstRow) / (double)rowSpan * trackWidth);

    /// <summary>
    /// The run a point falls on, taken from the row it sits over and which track it is on
    /// </summary>
    /// <summary>
    /// The row a point sits over, which is the reading the tracks are laid out to give
    /// </summary>
    public static int GetRow(float x, float width, int firstRow, int rowSpan)
    {
        var trackWidth = width - GutterWidth;

        return trackWidth <= 0 || rowSpan <= 0
            ? -1
            : firstRow + (int)((x - GutterWidth) / trackWidth * rowSpan);
    }

    public static int GetRunIndex(IReadOnlyList<RleRunDetail> runs,
                                  float x,
                                  float y,
                                  float width,
                                  int firstRow,
                                  int rowSpan)
    {
        var trackWidth = width - GutterWidth;

        if (trackWidth <= 0 || rowSpan <= 0 || x < GutterWidth)
        {
            return -1;
        }

        var isBitpacked = y > TrackHeight + (TrackGap / 2);

        var row = firstRow + (int)((x - GutterWidth) / trackWidth * rowSpan);

        for (var i = 0; i < runs.Count; i++)
        {
            if (runs[i].IsBitpacked == isBitpacked
                && row >= runs[i].StartRow
                && row < runs[i].StartRow + runs[i].Count)
            {
                return i;
            }
        }

        return -1;
    }

    public static int TotalRows(IReadOnlyList<RleRunDetail> runs)
        => runs.Count == 0 ? 0 : runs[^1].StartRow + runs[^1].Count;

    /// <summary>
    /// Places the values either kind of run holds on the hue wheel, each kind scaled over its own range
    /// </summary>
    private readonly record struct RunScale(long LiteralMin, long LiteralMax, long BitpackMin, long BitpackMax)
    {
        public static RunScale Build(IReadOnlyList<RleRunDetail> runs)
        {
            long literalMin = long.MaxValue, literalMax = long.MinValue;
            long bitpackMin = long.MaxValue, bitpackMax = long.MinValue;

            foreach (var run in runs)
            {
                if (run.IsBitpacked)
                {
                    bitpackMin = Math.Min(bitpackMin, run.Value);
                    bitpackMax = Math.Max(bitpackMax, run.Value);
                }
                else
                {
                    literalMin = Math.Min(literalMin, run.Value);
                    literalMax = Math.Max(literalMax, run.Value);
                }
            }

            return new RunScale(literalMin, literalMax, bitpackMin, bitpackMax);
        }

        public SKColor GetColour(RleRunDetail run)
        {
            var (min, max) = run.IsBitpacked ? (BitpackMin, BitpackMax) : (LiteralMin, LiteralMax);

            var hue = max > min ? (int)((run.Value - min) * (HueWheel - 1) / (max - min)) : HueWheel / 2;

            return run.IsBitpacked
                ? ColourHelpers.HsvToColor(hue, BitpackSaturation, BitpackValue).ToSkColor()
                : ColourHelpers.HsvToColor(hue, LiteralSaturation, LiteralValue).ToSkColor();
        }
    }

    public void Dispose()
    {
        _fill.Dispose();
        _marker.Dispose();
        _stroke.Dispose();
        _text.Dispose();
        _font.Dispose();
    }
}
