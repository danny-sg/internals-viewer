using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Windows.Foundation;
using Windows.UI.Text;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace InternalsViewer.UI.App.Controls.Page;

public sealed partial class HexViewControl
{
    public class MouseOverInfo(int? offset, Marker? marker)
    {
        public int? Offset { get; } = offset;

        public Marker? Marker { get; } = marker;

        public bool HasMarker => Marker != null;
    }

    // 16 bytes per line is the conventional way of displaying hex
    private const int BytesPerLine = 16;

    // Bytes are represented by 2 characters and a space
    const int CharactersPerByte = 2;

    public HexControlViewModel ViewModel { get; } = new();

    public HexViewControl()
    {
        InitializeComponent();

        SetAddress();

        MouseOver = new(default, default);

        // Border positions depend on the rendered text layout, so a resize/re-layout invalidates them
        HexRichTextBlock.SizeChanged += (_, _) => DrawChangeSpans();
    }

    private void SetAddress()
    {
        var stringBuilder = new StringBuilder();

        for (var i = 0; i < PageData.Size / BytesPerLine; i++)
        {
            stringBuilder.AppendLine($"{i * BytesPerLine:X8}");
        }

        AddressTextBlock.Text = stringBuilder.ToString();
    }

    public byte[] Data
    {
        get { return (byte[])GetValue(DataProperty); }
        set { SetValue(DataProperty, value); }
    }

    public static readonly DependencyProperty DataProperty = DependencyProperty
        .Register(nameof(Data),
            typeof(byte[]),
            typeof(HexViewControl),
            new PropertyMetadata(default, OnDataChanged));

    public ObservableCollection<Marker>? Markers
    {
        get { return (ObservableCollection<Marker>)GetValue(MarkersProperty); }
        set { SetValue(MarkersProperty, value); }
    }

    public static readonly DependencyProperty MarkersProperty = DependencyProperty
        .Register(nameof(Data),
            typeof(ObservableCollection<Marker>),
            typeof(HexViewControl),
            new PropertyMetadata(default, OnMarkersChanged));

    public Marker? SelectedMarker
    {
        get => (Marker?)GetValue(SelectedMarkerProperty);
        set => SetValue(SelectedMarkerProperty, value);
    }

    public static readonly DependencyProperty SelectedMarkerProperty
        = DependencyProperty.Register(nameof(SelectedMarker),
            typeof(Marker),
            typeof(HexViewControl),
            new PropertyMetadata(default, OnSelectedMarkerChanged));

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
            ScrollToPosition(control, span.Offset);
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
            ScrollToPosition((HexViewControl)d, offset);
        }
    }

    private double[]? _columnPositions;

    private double _byteWidth;

    /// <summary>
    /// Measures the x position of each hex column and the width of a two character byte
    /// </summary>
    /// <remarks>
    /// Column positions are measured as full prefix strings rather than multiplying a single character width -
    /// fractional advance widths accumulate, so a multiplied width drifts by several pixels at the high columns.
    /// Measured with a detached TextBlock so drawing does not depend on the RichTextBlock's layout being complete.
    /// </remarks>
    private void EnsureHexMetrics()
    {
        if (_columnPositions is not null)
        {
            return;
        }

        double Measure(string text)
        {
            var measure = new TextBlock
            {
                Text = text,
                FontFamily = HexRichTextBlock.FontFamily,
                FontSize = HexRichTextBlock.FontSize
            };

            measure.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

            return measure.DesiredSize.Width;
        }

        _byteWidth = Measure("00");

        _columnPositions = new double[BytesPerLine];

        for (var column = 1; column < BytesPerLine; column++)
        {
            // Trailing spaces are trimmed from the measured size, so the prefix is measured as the same number of
            // characters without the byte separator spaces - identical width in a monospace font
            _columnPositions[column] = Measure(new string('0', column * (CharactersPerByte + 1)));
        }
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

        EnsureHexMetrics();

        // The nominal LineHeight drifts from the rendered line pitch by a fraction of a pixel, which accumulates to
        // rows of error by the bottom of the page, so the pitch is derived from the actual rendered height. The
        // hex text ends with a trailing NewLine, so the layout contains one extra empty line beyond the data lines.
        var totalLines = Math.Max(1, (Data?.Length ?? PageData.Size) / BytesPerLine) + 1;

        var lineHeight = HexRichTextBlock.ActualHeight > 0
            ? HexRichTextBlock.ActualHeight / totalLines
            : HexRichTextBlock.LineHeight;

        var selectedBrush = new SolidColorBrush(Colors.OrangeRed);

        var defaultBrush = new SolidColorBrush(Colors.Gray);

        foreach (var changeSpan in spans)
        {
            var borderBrush = ReferenceEquals(changeSpan, SelectedChangeSpan) ? selectedBrush : defaultBrush;

            var start = changeSpan.Offset;
            var end = changeSpan.Offset + changeSpan.Length - 1;

            if (changeSpan.Length <= 0 || start < 0 || end >= PageData.Size)
            {
                continue;
            }

            var firstLine = start / BytesPerLine;
            var lastLine = end / BytesPerLine;

            // Rounded to whole pixels so the strokes render crisp instead of anti-aliased across two pixels
            var xStart = Math.Round(_columnPositions![start % BytesPerLine]) - 3;
            var xEnd = Math.Round(_columnPositions[end % BytesPerLine] + _byteWidth) + 2;

            var xLineStart = Math.Round(_columnPositions[0]) - 3;
            var xLineEnd = Math.Round(_columnPositions[BytesPerLine - 1] + _byteWidth) + 2;

            var yTop = Math.Round(firstLine * lineHeight) - 1;
            var yBottom = Math.Round((lastLine + 1) * lineHeight) + 1;

            void Add(Shape shape)
            {
                ToolTipService.SetToolTip(shape, changeSpan.Description);

                HexOverlayCanvas.Children.Add(shape);
            }

            Rectangle CreateBox(double left, double top, double width, double height)
            {
                var box = new Rectangle
                {
                    Width = width,
                    Height = height,
                    Stroke = borderBrush,
                    StrokeThickness = 2,
                    RadiusX = 2,
                    RadiusY = 2,
                    IsHitTestVisible = false,
                };

                Canvas.SetLeft(box, left);
                Canvas.SetTop(box, top);

                return box;
            }

            if (firstLine == lastLine)
            {
                Add(CreateBox(xStart, yTop, xEnd - xStart, yBottom - yTop));
            }
            else if (lastLine == firstLine + 1 && xStart > xEnd)
            {
                var firstBottom = Math.Round((firstLine + 1) * lineHeight) + 1;

                var lastTop = Math.Round(lastLine * lineHeight) - 1;

                Add(CreateBox(xStart, yTop, xLineEnd - xStart, firstBottom - yTop));
                Add(CreateBox(xLineStart, lastTop, xEnd - xLineStart, yBottom - lastTop));
            }
            else
            {
                var yFirstBottom = Math.Round((firstLine + 1) * lineHeight);
                var yLastTop = Math.Round(lastLine * lineHeight);

                var polygon = new Polygon
                {
                    Stroke = borderBrush,
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    IsHitTestVisible = false
                };

                polygon.Points.Add(new Point(xStart, yTop));
                polygon.Points.Add(new Point(xLineEnd, yTop));
                polygon.Points.Add(new Point(xLineEnd, yLastTop));
                polygon.Points.Add(new Point(xEnd, yLastTop));
                polygon.Points.Add(new Point(xEnd, yBottom));
                polygon.Points.Add(new Point(xLineStart, yBottom));
                polygon.Points.Add(new Point(xLineStart, yFirstBottom));
                polygon.Points.Add(new Point(xStart, yFirstBottom));

                Add(polygon);
            }
        }
    }

    public MouseOverInfo MouseOver
    {
        get => (MouseOverInfo)GetValue(MouseOverProperty);
        set => SetValue(MouseOverProperty, value);
    }

    public static readonly DependencyProperty MouseOverProperty
        = DependencyProperty.Register(nameof(MouseOver),
            typeof(MouseOverInfo),
            typeof(HexViewControl),
            new PropertyMetadata(default, null));

    private static void OnSelectedMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HexViewControl)d;

        if (e.NewValue is Marker marker)
        {
            ScrollToPosition(control, marker.StartPosition);
        }

        SetHexData(control.Data, control);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SetHexData(e.NewValue as byte[] ?? [], (HexViewControl)d);
    }

    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        HighlightMarkers((HexViewControl)d, (ObservableCollection<Marker>)e.NewValue);
    }

    private static void SetHexData(IReadOnlyList<byte> data, HexViewControl target)
    {
        var paragraph = new Paragraph();

        var stringBuilder = new StringBuilder();

        var position = 0;

        for (var line = 0; line < data.Count / BytesPerLine; line++)
        {
            for (var byteIndex = 0; byteIndex < BytesPerLine; byteIndex++)
            {
                if (position == target.SelectedMarker?.StartPosition)
                {
                    // Flush current run, replace with selection inline
                    paragraph.Inlines.Add(FlushRun(stringBuilder));
                }

                stringBuilder.Append(StringHelpers.ToHexString(data[position]));

                if (position == target.SelectedMarker?.EndPosition)
                {
                    // Flush current run with selection formatting
                    paragraph.Inlines.Add(FlushSelectionRun(stringBuilder, target.SelectedMarker));
                }

                // Add a space between bytes, but not for the last byte of the line
                if (byteIndex != 15)
                {
                    stringBuilder.Append(" ");
                }

                position++;
            }

            stringBuilder.Append(Environment.NewLine);
        }

        paragraph.Inlines.Add(FlushRun(stringBuilder));

        target.HexRichTextBlock.Blocks.Clear();
        target.HexRichTextBlock.Blocks.Add(paragraph);

        HighlightMarkers(target, target.Markers);

        target.DrawChangeSpans();
    }

    private static Inline FlushRun(StringBuilder stringBuilder)
    {
        var run = new Run { Text = stringBuilder.ToString() };

        stringBuilder.Clear();

        return run;
    }

    private static Inline FlushSelectionRun(StringBuilder stringBuilder, Marker marker)
    {
        var run = new Run { Text = stringBuilder.ToString(), TextDecorations = TextDecorations.Underline, FontWeight = FontWeights.Bold };

        stringBuilder.Clear();

        return run;
    }

    private static Inline FlushSelectionContainerRun(StringBuilder stringBuilder, Marker marker)
    {
        var container = new InlineUIContainer();

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Colors.Navy),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(marker.BackColour)
        };

        border.VerticalAlignment = VerticalAlignment.Top;

        var textRun = new TextBlock { Text = stringBuilder.ToString() };

        textRun.Foreground = new SolidColorBrush(marker.ForeColour);

        textRun.TextWrapping = TextWrapping.Wrap;

        container.Child = border;

        border.Child = textRun;

        stringBuilder.Clear();

        return container;
    }

    private static void HighlightMarkers(HexViewControl target, ObservableCollection<Marker>? markers)
    {
        target.HexRichTextBlock.TextHighlighters.Clear();

        void Highlight(Marker source)
        {
            var start = ToRunPosition(source.StartPosition);
            var end = ToRunPosition(source.EndPosition + 1) - 1;

            var length = end - start;

            var foregroundColour = source.ForeColour;
            var backgroundColour = source.BackColour;

            var highlighter = new TextHighlighter
            {
                Foreground = new SolidColorBrush(foregroundColour),
                Background = new SolidColorBrush(backgroundColour),

                Ranges = { new TextRange(start, length) }
            };

            target.HexRichTextBlock.TextHighlighters.Add(highlighter);

            if (source.Children.Any())
            {
                foreach (var child in source.Children)
                {
                    Highlight(child);
                }
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
    private static void ScrollToPosition(HexViewControl target, int position)
    {
        const int totalLines = PageData.Size / BytesPerLine;

        var positionLineNumber = (position / BytesPerLine) - 1;

        var heightPerLine = target.HexRichTextBlock.ActualHeight / totalLines;

        var scrollPosition = positionLineNumber * heightPerLine;

        target.ScrollViewer.ScrollToVerticalOffset(scrollPosition);
    }

    /// <summary>
    /// Converts a byte position to a position in the hex text block
    /// </summary>
    private static int ToRunPosition(int position)
    {
        // Bytes are represented by 2 characters and a space
        const int charactersPerByte = 3;

        var lineNumber = position / BytesPerLine;

        return position * charactersPerByte + lineNumber * (Environment.NewLine.Length - 1);
    }

    /// <summary>
    /// Converts a position in the hex text block to a byte position
    /// </summary>
    private static int FromRunPosition(int position, decimal charactersPerLine)
    {
        var lineNumber = (int)Math.Floor(position / charactersPerLine);

        var linePosition = position % charactersPerLine;
        var bytePosition = Math.Round(linePosition / (CharactersPerByte + 1));

        return lineNumber * BytesPerLine + (int)bytePosition;
    }

    private void HexRichTextBlock_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var rect = HexRichTextBlock.SelectionEnd.GetCharacterRect(LogicalDirection.Forward);

        SelectionInfoPopup.HorizontalOffset = rect.X + 4;
        SelectionInfoPopup.VerticalOffset = rect.Y;

        ViewModel.StartOffset = FromRunPosition(HexRichTextBlock.SelectionStart.Offset,
                                                BytesPerLine * CharactersPerByte // Bytes
                                                 + BytesPerLine - 1               // Spaces in between bytes (except last byte)
                                                 + Environment.NewLine.Length);

        ViewModel.EndOffset = FromRunPosition(HexRichTextBlock.SelectionEnd.Offset,
                                                BytesPerLine * CharactersPerByte // Bytes
                                              + BytesPerLine - 1               // Spaces in between bytes (except last byte)
                                              + Environment.NewLine.Length);

        ViewModel.SelectedText = HexRichTextBlock.SelectedText;
    }

    private void HexRichTextBlock_LostFocus(object sender, RoutedEventArgs e)
    {
        SelectionInfoPopup.IsOpen = false;
    }

    private void HexRichTextBlock_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var position = HexRichTextBlock.GetPositionFromPoint(e.GetCurrentPoint(HexRichTextBlock).Position);

        if (position != null)
        {
            var offset = FromRunPosition(position.Offset, BytesPerLine * CharactersPerByte // Bytes
                                                          + BytesPerLine - 1               // Spaces in between bytes (except last byte)
                                                          + Environment.NewLine.Length) - 1;

            var marker = Markers?.FirstOrDefault(m => m.StartPosition <= offset && m.EndPosition >= offset);

            MouseOver = new(offset, marker);
        }
    }

    private void HexRichTextBlock_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Arrow));

        e.Handled = true;
    }

    private void HexRichTextBlock_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        MouseOver = new(default, default);
    }
}