using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.UI.App.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using InternalsViewer.UI.App.Models.Logging;

namespace InternalsViewer.UI.App.Controls.HexView;

public sealed partial class HexViewControl
{
    private static readonly Dictionary<Color, SolidColorBrush> BrushCache = [];

    private static SolidColorBrush GetBrush(Color colour)
    {
        if (!BrushCache.TryGetValue(colour, out var brush))
        {
            brush = new SolidColorBrush(colour);

            BrushCache[colour] = brush;
        }

        return brush;
    }

    public ObservableCollection<LogRecordAnnotation>? ChangeSpans
    {
        get => (ObservableCollection<LogRecordAnnotation>?)GetValue(ChangeSpansProperty);
        set => SetValue(ChangeSpansProperty, value);
    }

    public static readonly DependencyProperty ChangeSpansProperty = DependencyProperty
        .Register(nameof(ChangeSpans),
            typeof(ObservableCollection<LogRecordAnnotation>),
            typeof(HexViewControl),
            new PropertyMetadata(null, OnChangeSpansChanged));

    private static void OnChangeSpansChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((HexViewControl)d).DrawChangeSpans();
    }

    public LogRecordAnnotation? SelectedChangeSpan
    {
        get => (LogRecordAnnotation?)GetValue(SelectedChangeSpanProperty);
        set => SetValue(SelectedChangeSpanProperty, value);
    }

    public static readonly DependencyProperty SelectedChangeSpanProperty = DependencyProperty
        .Register(nameof(SelectedChangeSpan),
            typeof(LogRecordAnnotation),
            typeof(HexViewControl),
            new PropertyMetadata(null, OnSelectedChangeSpanChanged));

    private static void OnSelectedChangeSpanChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HexViewControl)d;

        if (e.NewValue is LogRecordAnnotation span)
        {
            ScrollToPosition(control, span.Offset, isFollowingSelection: false);
        }

        control.DrawChangeSpans();
    }

    public int? ScrollToOffset
    {
        get => (int?)GetValue(ScrollToOffsetProperty);
        set => SetValue(ScrollToOffsetProperty, value);
    }

    public static readonly DependencyProperty ScrollToOffsetProperty = DependencyProperty
        .Register(nameof(ScrollToOffset),
            typeof(int?),
            typeof(HexViewControl),
            new PropertyMetadata(null, OnScrollToOffsetChanged));

    private static void OnScrollToOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is int offset)
        {
            ScrollToPosition((HexViewControl)d, offset, isFollowingSelection: false);
        }
    }

    private HexMetrics? _metrics;

    /// <summary>
    /// Measures the hex columns, which is done once and against a detached TextBlock
    /// </summary>
    /// <remarks>
    /// Measuring separately keeps the drawing from depending on the RichTextBlock's own layout being complete.
    /// </remarks>
    private HexMetrics EnsureMetrics() => _metrics ??= HexMetrics.Measure(MeasureText);

    private double MeasureText(string text)
    {
        var measure = new TextBlock
        {
            Text = text,
            FontFamily = HexRichTextBlock.FontFamily,
            FontSize = HexRichTextBlock.FontSize
        };

        measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        return measure.DesiredSize.Width;
    }

    /// <summary>
    /// Draws a border on the overlay canvas around the hex bytes each change span covers
    /// </summary>
    /// <remarks>
    /// Byte n sits at column n % 16 on line n / 16. A span within one display line is a rectangle; a span crossing
    /// lines is drawn as a single outline polygon around the covered region, so there are no border lines between
    /// the rows it spans.
    /// </remarks>
    private void DrawChangeSpans()
    {
        HexOverlayCanvas.Children.Clear();

        if (HexRichTextBlock.Blocks.Count == 0)
        {
            return;
        }

        var spans = (ChangeSpans ?? Enumerable.Empty<LogRecordAnnotation>()).ToList();

        // The selected span is moved to the end so it draws last, on top of any overlapping spans
        if (SelectedChangeSpan is not null)
        {
            spans.Remove(SelectedChangeSpan);

            spans.Add(SelectedChangeSpan);
        }

        if (spans.Count == 0)
        {
            return;
        }

        var metrics = EnsureMetrics();

        var lineHeight = GetLineHeight();

        var selectedBrush = GetBrush(Colors.OrangeRed);

        var defaultBrush = GetBrush(Colors.Gray);

        foreach (var changeSpan in spans)
        {
            var borderBrush = ReferenceEquals(changeSpan, SelectedChangeSpan) ? selectedBrush : defaultBrush;

            var start = changeSpan.Offset;
            var end = changeSpan.Offset + changeSpan.Length - 1;

            if (changeSpan.Length <= 0 || start < 0 || end >= PageData.Size)
            {
                continue;
            }

            var border = metrics.GetSpanBorder(start, end, lineHeight);

            foreach (var box in border.Boxes)
            {
                Add(CreateBox(box));
            }

            if (border.Outline.Count > 0)
            {
                var polygon = new Polygon
                {
                    Stroke = borderBrush,
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    IsHitTestVisible = false
                };

                foreach (var point in border.Outline)
                {
                    polygon.Points.Add(point);
                }

                Add(polygon);
            }

            continue;

            Rectangle CreateBox(Rect bounds)
            {
                var box = new Rectangle
                {
                    Width = bounds.Width,
                    Height = bounds.Height,
                    Stroke = borderBrush,
                    StrokeThickness = 2,
                    RadiusX = 2,
                    RadiusY = 2,
                    IsHitTestVisible = false,
                };

                Canvas.SetLeft(box, bounds.Left);
                Canvas.SetTop(box, bounds.Top);

                return box;
            }

            void Add(Shape shape)
            {
                ToolTipService.SetToolTip(shape, changeSpan.Description);

                HexOverlayCanvas.Children.Add(shape);
            }
        }
    }

    /// <summary>
    /// Darkens everything but the selected marker, which is cut out of the mask and left as it was
    /// </summary>
    /// <remarks>
    /// The cutout is an even odd hole in a rectangle covering the text, so the bytes under it keep their marker
    /// colours. Nothing on the mask is hit testable, leaving the text underneath selectable, and a click anywhere
    /// on the hex clears the selection the mask was drawn for.
    /// </remarks>
    private void DrawSelectionMask()
    {
        SelectionMaskCanvas.Children.Clear();

        if (SelectedMarker is not { } marker || HexRichTextBlock.Blocks.Count == 0 || IsScrolling)
        {
            return;
        }

        var length = Data?.Length ?? 0;

        var start = marker.StartPosition;

        var end = Math.Min(marker.EndPosition, length - 1);

        // A marker outside the window has no position, and one held for context has nothing on screen to cut out
        if (start < 0 || start >= length || end < start)
        {
            return;
        }

        var width = HexRichTextBlock.ActualWidth;

        var height = HexRichTextBlock.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var metrics = EnsureMetrics();

        var bounds = new Rect(0, 0, width, height);

        var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };

        geometry.Children.Add(new RectangleGeometry { Rect = bounds });

        foreach (var cutout in metrics.GetSpanRects(start, end, GetLineHeight()))
        {
            var clamped = HexMetrics.Clamp(cutout, bounds);

            if (clamped.Width > 0 && clamped.Height > 0)
            {
                geometry.Children.Add(new RectangleGeometry { Rect = clamped });
            }
        }

        SelectionMaskCanvas.Children.Add(new Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(ActualTheme == ElementTheme.Light
                                           ? Color.FromArgb(90, 0, 0, 0)
                                           : Color.FromArgb(150, 0, 0, 0)),
            IsHitTestVisible = false
        });
    }

    /// <summary>
    /// The pitch the drawing lays out against, taken over the data on show
    /// </summary>
    private double GetLineHeight()
        => HexLayout.GetLineHeight(HexRichTextBlock.ActualHeight,
                                   Data?.Length ?? PageData.Size,
                                   HexRichTextBlock.LineHeight);

    private static void HighlightMarkers(HexViewControl target, ObservableCollection<Marker>? markers)
    {
        target.HexRichTextBlock.TextHighlighters.Clear();

        void Highlight(Marker source)
        {
            // A marker can be shown for context without being in the data on screen, and has nothing to highlight
            if (source.StartPosition < 0 || source.EndPosition < source.StartPosition)
            {
                return;
            }

            var start = HexLayout.ToRunPosition(source.StartPosition);
            var end = HexLayout.ToRunPosition(source.EndPosition + 1) - 1;

            var length = end - start;

            var foregroundColour = source.ForeColour;
            var backgroundColour = source.BackColour;

            var highlighter = new TextHighlighter
            {
                Foreground = GetBrush(foregroundColour),
                Background = GetBrush(backgroundColour),

                Ranges = { new TextRange(start, length) }
            };

            target.HexRichTextBlock.TextHighlighters.Add(highlighter);

            foreach (var child in source.Children)
            {
                Highlight(child);
            }
        }

        if (markers != null)
        {
            foreach (var marker in markers)
            {
                Highlight(marker);
            }
        }
    }

    /// <summary>
    /// Scrolls the hex view to the specified offset position
    /// </summary>
    /// <remarks>
    /// There doesn't seem to be a ScrollToPosition type method built into the RichTextBlock control.
    /// 
    /// This works by taking the height of the text and dividing by the know number of lines to get the height of each line, then 
    /// multiplying by the calculated line number to get the position.
    /// </remarks>
    private static void ScrollToPosition(HexViewControl target, int position, bool isFollowingSelection)
    {
        // A virtualized control holds only the lines already on screen, and a marker outside the window has no position
        if (target.IsVirtualized || position < 0)
        {
            return;
        }

        const int totalLines = PageData.Size / BytesPerLine;

        var positionLineNumber = (position / BytesPerLine) - 1;

        var heightPerLine = target.HexRichTextBlock.ActualHeight / totalLines;

        var scrollPosition = positionLineNumber * heightPerLine;

        // A scroll of nothing raises no view change to be told apart from one the reader made
        if (Math.Abs(scrollPosition - target.ScrollViewer.VerticalOffset) < 1)
        {
            return;
        }

        target._isScrollingToSelection = isFollowingSelection;

        target.ScrollViewer.ScrollToVerticalOffset(scrollPosition);
    }
}
