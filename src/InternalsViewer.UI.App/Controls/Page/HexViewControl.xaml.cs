using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Windows.Foundation;
using Windows.UI.Text;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
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

    // Matches the RichTextBlock LineHeight the hex is laid out with
    private const double LineHeight = 16;

    private const int WheelDeltaPerNotch = 120;

    private const int LinesPerNotch = 3;

    public HexControlViewModel ViewModel { get; } = new();

    public HexViewControl()
    {
        InitializeComponent();

        SetAddress();

        MouseOver = new(default, default);

        // Border positions depend on the rendered text layout, so a resize/re-layout invalidates them
        HexRichTextBlock.SizeChanged += (_, _) => DrawChangeSpans();

        ScrollViewer.SizeChanged += (_, _) => UpdateVirtualization();

        // The virtualized window holds only the lines on screen, so the ScrollViewer has nothing of its own to scroll
        ScrollViewer.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), true);

        // A drag that ends without its closing scroll would leave the window held, so the pointer ends it too
        VirtualScrollBar.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(OnScrollBarCaptureLost), true);
    }

    private void OnScrollBarCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (IsScrolling)
        {
            EndScroll(_pendingScrollLine);
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!IsVirtualized)
        {
            return;
        }

        var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;

        ScrollWindowByLines(-delta / WheelDeltaPerNotch * LinesPerNotch);

        e.Handled = true;
    }

    /// <summary>
    /// Moves the window by whole lines, keeping it inside the structure the scroll bar was sized for
    /// </summary>
    private void ScrollWindowByLines(int lines)
    {
        var line = (WindowOffset / BytesPerLine) + lines;

        WindowOffset = Math.Clamp(line, 0, (int)VirtualScrollBar.Maximum) * BytesPerLine;
    }

    /// <summary>
    /// Builds the address column for whatever length of data is shown, offset by <see cref="BaseAddress"/>
    /// </summary>
    /// <remarks>
    /// A page is always the same size, however a columnstore blob region is not, and starts part way into the blob it
    /// was taken from, so the address has to say where in that blob a line sits rather than where in the slice.
    /// </remarks>
    private void SetAddress()
    {
        var length = Data is { Length: > 0 } ? Data.Length : PageData.Size;

        var lineCount = (length + BytesPerLine - 1) / BytesPerLine;

        // A held drag leaves the bytes where they were, so the addresses are the only sign of where it has reached
        var baseAddress = IsScrolling ? _pendingScrollLine * BytesPerLine : BaseAddress;

        var stringBuilder = new StringBuilder();

        for (var i = 0; i < lineCount; i++)
        {
            // Separator rather than terminator - a trailing newline renders as an extra blank address line
            if (i > 0)
            {
                stringBuilder.AppendLine();
            }

            stringBuilder.Append($"{baseAddress + (i * BytesPerLine):X8}");
        }

        AddressTextBlock.Text = stringBuilder.ToString();

        SetAreas(baseAddress, lineCount);
    }

    /// <summary>
    /// Names the areas the window covers, which is the only map of the blob a held drag has to steer by
    /// </summary>
    /// <remarks>
    /// A name is written where its area starts rather than against every line, so the column reads as a map of
    /// where the window has reached rather than a wall of repeated text.
    /// </remarks>
    private void SetAreas(int baseAddress, int lineCount)
    {
        AreaOverlay.Visibility = IsScrolling && Areas is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;

        AreaOverlay.Children.Clear();

        if (AreaOverlay.Visibility == Visibility.Collapsed || Areas is not { } areas)
        {
            return;
        }

        var previous = string.Empty;

        for (var i = 0; i < lineCount; i++)
        {
            var name = NameAt(areas, baseAddress + (i * BytesPerLine));

            if (name.Length > 0 && name != previous)
            {
                AreaOverlay.Children.Add(AreaLabel(name, i));
            }

            previous = name;
        }
    }

    /// <summary>
    /// One area name, sitting over the bytes on the line its area starts at
    /// </summary>
    private static Border AreaLabel(string name, int line) => new()
    {
        Background = new SolidColorBrush(Colors.White),
        CornerRadius = new CornerRadius(2),
        Padding = new Thickness(4, 0, 4, 0),
        Margin = new Thickness(0, line * LineHeight, 12, 0),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Child = new TextBlock
        {
            Text = name,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.Black),
            LineHeight = LineHeight
        }
    };

    private static string NameAt(IReadOnlyList<HexArea> areas, int offset)
    {
        var name = string.Empty;

        foreach (var area in areas)
        {
            if (area.Start > offset)
            {
                break;
            }

            name = area.Name;
        }

        return name;
    }

    /// <summary>
    /// Named stretches of the data, in order, each running until the next one starts
    /// </summary>
    public IReadOnlyList<HexArea>? Areas
    {
        get => (IReadOnlyList<HexArea>?)GetValue(AreasProperty);
        set => SetValue(AreasProperty, value);
    }

    public static readonly DependencyProperty AreasProperty
        = DependencyProperty.Register(nameof(Areas),
            typeof(IReadOnlyList<HexArea>),
            typeof(HexViewControl),
            new PropertyMetadata(null));

    /// <summary>
    /// Shows only as many lines as fit, scrolling by moving the window rather than the content
    /// </summary>
    /// <remarks>
    /// A page fits in one layout pass, a columnstore blob does not - megabytes of runs will not lay out at all. When
    /// virtualised the owner supplies just the window the control asks for, which keeps the rendered run count to
    /// whatever is on screen however large the structure behind it is.
    /// </remarks>
    public bool IsVirtualized
    {
        get => (bool)GetValue(IsVirtualizedProperty);
        set => SetValue(IsVirtualizedProperty, value);
    }

    public static readonly DependencyProperty IsVirtualizedProperty
        = DependencyProperty.Register(nameof(IsVirtualized),
            typeof(bool),
            typeof(HexViewControl),
            new PropertyMetadata(false, OnVirtualizationChanged));

    /// <summary>
    /// Total bytes of the structure being windowed, which sizes the scroll bar
    /// </summary>
    public int TotalLength
    {
        get => (int)GetValue(TotalLengthProperty);
        set => SetValue(TotalLengthProperty, value);
    }

    public static readonly DependencyProperty TotalLengthProperty
        = DependencyProperty.Register(nameof(TotalLength),
            typeof(int),
            typeof(HexViewControl),
            new PropertyMetadata(0, OnVirtualizationChanged));

    /// <summary>
    /// Byte offset of the window the owner should supply, always on a line boundary
    /// </summary>
    public int WindowOffset
    {
        get => (int)GetValue(WindowOffsetProperty);
        set => SetValue(WindowOffsetProperty, value);
    }

    public static readonly DependencyProperty WindowOffsetProperty
        = DependencyProperty.Register(nameof(WindowOffset),
            typeof(int),
            typeof(HexViewControl),
            new PropertyMetadata(0, OnWindowOffsetChanged));

    /// <summary>
    /// Follows the window when something other than the scroll bar moves it, such as jumping to a region
    /// </summary>
    private static void OnWindowOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HexViewControl)d;

        if (!control.IsVirtualized)
        {
            return;
        }

        var line = (int)e.NewValue / BytesPerLine;

        control.VirtualScrollBar.Value = Math.Clamp(line, 0, (int)control.VirtualScrollBar.Maximum);
    }

    /// <summary>
    /// Bytes the window should hold, being the lines that fit plus one so scrolling does not reveal a gap
    /// </summary>
    public int WindowLength
    {
        get => (int)GetValue(WindowLengthProperty);
        set => SetValue(WindowLengthProperty, value);
    }

    public static readonly DependencyProperty WindowLengthProperty
        = DependencyProperty.Register(nameof(WindowLength),
            typeof(int),
            typeof(HexViewControl),
            new PropertyMetadata(0));

    private static void OnVirtualizationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HexViewControl)d).UpdateVirtualization();

    private void UpdateVirtualization()
    {
        if (!IsVirtualized)
        {
            VirtualScrollBar.Visibility = Visibility.Collapsed;

            ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

            return;
        }

        VirtualScrollBar.Visibility = Visibility.Visible;

        ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

        var visibleLines = Math.Max(1, (int)(ScrollViewer.ActualHeight / LineHeight));

        WindowLength = (visibleLines + 1) * BytesPerLine;

        var totalLines = (TotalLength + BytesPerLine - 1) / BytesPerLine;

        VirtualScrollBar.Maximum = Math.Max(0, totalLines - visibleLines);
        VirtualScrollBar.ViewportSize = visibleLines;
        VirtualScrollBar.SmallChange = 1;
        VirtualScrollBar.LargeChange = visibleLines;
    }

    /// <summary>
    /// Moves the window the scroll bar asks for, a drag being held until it ends
    /// </summary>
    /// <remarks>
    /// A thumb drag raises a scroll for every pixel it passes, and each one reloads the window and rebuilds every
    /// marker over it. Holding the drag lets it run at the speed of the mouse and pays the cost once, on release.
    /// </remarks>
    private void VirtualScrollBar_OnScroll(object sender, ScrollEventArgs e)
    {
        if (e.ScrollEventType == ScrollEventType.ThumbTrack)
        {
            IsScrolling = true;

            _pendingScrollLine = (int)e.NewValue;

            SetAddress();

            return;
        }

        EndScroll((int)e.NewValue);
    }

    private void EndScroll(int line)
    {
        IsScrolling = false;

        WindowOffset = Math.Clamp(line, 0, (int)VirtualScrollBar.Maximum) * BytesPerLine;

        // Dragging back to where it started moves nothing, leaving the preview addresses to be put back by hand
        SetAddress();
    }

    private int _pendingScrollLine;

    private bool _isScrolling;

    /// <summary>
    /// Whether a drag is in progress, the bytes on show being the ones it started from until it ends
    /// </summary>
    private bool IsScrolling
    {
        get => _isScrolling;
        set
        {
            if (_isScrolling == value)
            {
                return;
            }

            _isScrolling = value;

            HexRichTextBlock.Opacity = value ? ScrollingOpacity : 1;
        }
    }

    private const double ScrollingOpacity = 0.4;

    /// <summary>
    /// Offset the address column counts from, so a slice of a larger structure shows its true offsets
    /// </summary>
    public int BaseAddress
    {
        get => (int)GetValue(BaseAddressProperty);
        set => SetValue(BaseAddressProperty, value);
    }

    public static readonly DependencyProperty BaseAddressProperty
        = DependencyProperty.Register(nameof(BaseAddress),
            typeof(int),
            typeof(HexViewControl),
            new PropertyMetadata(0, OnBaseAddressChanged));

    private static void OnBaseAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HexViewControl)d).InvalidateHexData();

    private bool _isHexRebuildPending;

    /// <summary>
    /// Asks for one rebuild of the hex text at the end of the current pass, however many properties moved
    /// </summary>
    /// <remarks>
    /// Moving the window sets the base address, the data and the selected marker, and each of those on its own used
    /// to rebuild the whole run list. Coalescing them leaves one rebuild per window move rather than three.
    /// </remarks>
    private void InvalidateHexData()
    {
        if (_isHexRebuildPending)
        {
            return;
        }

        _isHexRebuildPending = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            _isHexRebuildPending = false;

            SetHexData(Data ?? [], this);

            SetAddress();
        });
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
        .Register(nameof(Markers),
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

        if (SelectionRange(e.OldValue as Marker) == SelectionRange(e.NewValue as Marker))
        {
            return;
        }

        control.InvalidateHexData();
    }

    private static (int Start, int End)? SelectionRange(Marker? marker)
    {
        return marker is null ? null : (marker.StartPosition, marker.EndPosition);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HexViewControl)d).InvalidateHexData();

    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HexViewControl)d;

        // A pending rebuild highlights from the new markers itself, so highlighting them twice is only wasted work
        if (control._isHexRebuildPending)
        {
            return;
        }

        HighlightMarkers(control, (ObservableCollection<Marker>)e.NewValue);
    }

    private static void SetHexData(IReadOnlyList<byte> data, HexViewControl target)
    {
        var runs = HexTextBuilder.Build(data,
                                        BytesPerLine,
                                        target.SelectedMarker?.StartPosition,
                                        target.SelectedMarker?.EndPosition);

        var paragraph = new Paragraph();

        foreach (var run in runs)
        {
            paragraph.Inlines.Add(CreateInline(run));
        }

        target.HexRichTextBlock.Blocks.Clear();
        target.HexRichTextBlock.Blocks.Add(paragraph);

        HighlightMarkers(target, target.Markers);

        target.DrawChangeSpans();
    }

    private static Inline CreateInline(HexRun run)
    {
        if (!run.IsSelected)
        {
            return new Run { Text = run.Text };
        }

        return new Run { Text = run.Text, TextDecorations = TextDecorations.Underline, FontWeight = FontWeights.Bold };
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
            // A marker can be shown for context without being in the data on screen, and has nothing to highlight
            if (source.StartPosition < 0 || source.EndPosition < source.StartPosition)
            {
                return;
            }

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
        // A virtualized control holds only the lines already on screen, and a marker outside the window has no position
        if (target.IsVirtualized || position < 0)
        {
            return;
        }

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

            MouseOver = new(offset, FindMarker(Markers, offset));
        }
    }

    /// <summary>
    /// The narrowest marker covering an offset, a field being wanted ahead of the section it sits in
    /// </summary>
    private static Marker? FindMarker(IEnumerable<Marker>? markers, int offset)
    {
        Marker? found = null;

        if (markers is null)
        {
            return null;
        }

        foreach (var marker in markers)
        {
            if (marker.StartPosition > offset || marker.EndPosition < offset)
            {
                continue;
            }

            var candidate = FindMarker(marker.Children, offset) ?? marker;

            if (found is null || candidate.Length < found.Length)
            {
                found = candidate;
            }
        }

        return found;
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