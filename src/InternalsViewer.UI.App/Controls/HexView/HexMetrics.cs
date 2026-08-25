using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.HexView;

/// <summary>
/// The shape a stretch of bytes takes on screen, worked out from the measured width of the hex columns
/// </summary>
/// <remarks>
/// Byte n sits at column n % 16 on line n / 16. Everything past the measurement itself is arithmetic, so the
/// drawing over the hex is separated from the text block it is drawn against.
/// </remarks>
public sealed class HexMetrics
{
    private const double LeadingPadding = 3;

    private const double TrailingPadding = 2;

    private const double VerticalPadding = 1;

    public double ByteWidth { get; }

    public IReadOnlyList<double> ColumnPositions { get; }

    private HexMetrics(double byteWidth, IReadOnlyList<double> columnPositions)
    {
        ByteWidth = byteWidth;

        ColumnPositions = columnPositions;
    }

    /// <summary>
    /// Measures the x position of each hex column and the width of a two character byte
    /// </summary>
    /// <remarks>
    /// Column positions are measured as full prefix strings rather than multiplying a single character width -
    /// fractional advance widths accumulate, so a multiplied width drifts by several pixels at the high columns.
    /// </remarks>
    public static HexMetrics Measure(Func<string, double> measureText)
    {
        var columnPositions = new double[HexLayout.BytesPerLine];

        for (var column = 1; column < HexLayout.BytesPerLine; column++)
        {
            // Trailing spaces are trimmed from the measured size, so the prefix is measured as the same number of
            // characters without the byte separator spaces - identical width in a monospace font
            columnPositions[column] = measureText(new string('0', column * (HexLayout.CharactersPerByte + 1)));
        }

        return new HexMetrics(measureText("00"), columnPositions);
    }

    /// <summary>
    /// The bytes a span covers as up to three rectangles, being the first line, the whole lines and the last line
    /// </summary>
    /// <remarks>
    /// They are kept from overlapping because an even odd fill would take an overlap back out of a hole again.
    /// </remarks>
    public IReadOnlyList<Rect> GetSpanRects(int start, int end, double lineHeight)
    {
        var firstLine = start / HexLayout.BytesPerLine;

        var lastLine = end / HexLayout.BytesPerLine;

        var top = GetTop(firstLine, lineHeight);

        var bottom = GetBottom(lastLine, lineHeight);

        if (firstLine == lastLine)
        {
            return [new Rect(GetLeft(start), top, GetRight(end) - GetLeft(start), bottom - top)];
        }

        var firstBottom = Math.Round((firstLine + 1) * lineHeight);

        var lastTop = Math.Round(lastLine * lineHeight);

        var rects = new List<Rect>
        {
            new(GetLeft(start), top, LineRight - GetLeft(start), firstBottom - top)
        };

        if (lastLine > firstLine + 1)
        {
            rects.Add(new Rect(LineLeft, firstBottom, LineRight - LineLeft, lastTop - firstBottom));
        }

        rects.Add(new Rect(LineLeft, lastTop, GetRight(end) - LineLeft, bottom - lastTop));

        return rects;
    }

    /// <summary>
    /// The outline of a span, drawn as boxes where it takes one or two lines and as a polygon where it takes more
    /// </summary>
    /// <remarks>
    /// A span crossing lines is outlined in one piece so there are no border lines between the rows it spans. Two
    /// lines whose bytes do not overlap horizontally have no such shape, so they are boxed separately instead.
    /// </remarks>
    public SpanBorder GetSpanBorder(int start, int end, double lineHeight)
    {
        var firstLine = start / HexLayout.BytesPerLine;

        var lastLine = end / HexLayout.BytesPerLine;

        var left = GetLeft(start);

        var right = GetRight(end);

        var top = GetTop(firstLine, lineHeight);

        var bottom = GetBottom(lastLine, lineHeight);

        if (firstLine == lastLine)
        {
            return new SpanBorder([new Rect(left, top, right - left, bottom - top)], []);
        }

        if (lastLine == firstLine + 1 && left > right)
        {
            var firstBottom = GetBottom(firstLine, lineHeight);

            var lastTop = GetTop(lastLine, lineHeight);

            return new SpanBorder(
            [
                new Rect(left, top, LineRight - left, firstBottom - top),
                new Rect(LineLeft, lastTop, right - LineLeft, bottom - lastTop)
            ], []);
        }

        var firstEdge = Math.Round((firstLine + 1) * lineHeight);

        var lastEdge = Math.Round(lastLine * lineHeight);

        return new SpanBorder([],
        [
            new Point(left, top),
            new Point(LineRight, top),
            new Point(LineRight, lastEdge),
            new Point(right, lastEdge),
            new Point(right, bottom),
            new Point(LineLeft, bottom),
            new Point(LineLeft, firstEdge),
            new Point(left, firstEdge)
        ]);
    }

    /// <summary>
    /// The part of a rectangle another one covers, which is empty where the two do not meet at all
    /// </summary>
    public static Rect Clamp(Rect rect, Rect bounds)
    {
        var left = Math.Max(rect.Left, bounds.Left);

        var top = Math.Max(rect.Top, bounds.Top);

        var right = Math.Min(rect.Right, bounds.Right);

        var bottom = Math.Min(rect.Bottom, bounds.Bottom);

        return right > left && bottom > top ? new Rect(left, top, right - left, bottom - top) : default;
    }

    private double LineLeft => GetLeft(0);

    private double LineRight => GetRight(HexLayout.BytesPerLine - 1);

    // Rounded to whole pixels so the strokes render crisp instead of anti-aliased across two pixels
    private double GetLeft(int position) => Math.Round(ColumnPositions[position % HexLayout.BytesPerLine]) - LeadingPadding;

    private double GetRight(int position)
        => Math.Round(ColumnPositions[position % HexLayout.BytesPerLine] + ByteWidth) + TrailingPadding;

    private static double GetTop(int line, double lineHeight) => Math.Round(line * lineHeight) - VerticalPadding;

    private static double GetBottom(int line, double lineHeight) => Math.Round((line + 1) * lineHeight) + VerticalPadding;
}

/// <summary>
/// How a span is outlined, one of the two being empty
/// </summary>
public sealed record SpanBorder(IReadOnlyList<Rect> Boxes, IReadOnlyList<Point> Outline);
