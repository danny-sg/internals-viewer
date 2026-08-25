using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.HexView;

public sealed partial class HexViewControl
{
    private const double ScrollingOpacity = 0.4;

    public static readonly DependencyProperty IsVirtualizedProperty
        = DependencyProperty.Register(nameof(IsVirtualized),
            typeof(bool),
            typeof(HexViewControl),
            new PropertyMetadata(false, OnVirtualizationChanged));

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

    public static readonly DependencyProperty TotalLengthProperty
        = DependencyProperty.Register(nameof(TotalLength),
            typeof(int),
            typeof(HexViewControl),
            new PropertyMetadata(0, OnVirtualizationChanged));

    /// <summary>
    /// Total bytes of the structure being windowed, which sizes the scroll bar
    /// </summary>
    public int TotalLength
    {
        get => (int)GetValue(TotalLengthProperty);
        set => SetValue(TotalLengthProperty, value);
    }

    public static readonly DependencyProperty WindowOffsetProperty
        = DependencyProperty.Register(nameof(WindowOffset),
            typeof(int),
            typeof(HexViewControl),
            new PropertyMetadata(0, OnWindowOffsetChanged));

    /// <summary>
    /// Byte offset of the window the owner should supply, always on a line boundary
    /// </summary>
    public int WindowOffset
    {
        get => (int)GetValue(WindowOffsetProperty);
        set => SetValue(WindowOffsetProperty, value);
    }

    public static readonly DependencyProperty WindowLengthProperty
        = DependencyProperty.Register(nameof(WindowLength),
            typeof(int),
            typeof(HexViewControl),
            new PropertyMetadata(0));

    /// <summary>
    /// Bytes the window should hold, being the lines that fit plus one so scrolling does not reveal a gap
    /// </summary>
    public int WindowLength
    {
        get => (int)GetValue(WindowLengthProperty);
        set => SetValue(WindowLengthProperty, value);
    }

    /// <summary>
    /// Set while the view is being moved to bring the selection into sight, which is not a scroll away from it
    /// </summary>
    private bool _isScrollingToSelection;

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

    /// <summary>
    /// Brings a settled scroll onto a line boundary, so the window starts on a whole row of bytes
    /// </summary>
    /// <remarks>
    /// The virtualized window is built a line at a time and is already aligned, its own scroll bar counting lines.
    /// </remarks>
    private void OnScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (IsVirtualized)
        {
            return;
        }

        if (_isScrollingToSelection)
        {
            if (!e.IsIntermediate)
            {
                _isScrollingToSelection = false;
            }
        }
        else if (SelectedMarker is not null)
        {
            // The mask marks a place in the window, so scrolling away from it takes the mask with it
            SelectedMarker = null;
        }

        if (e.IsIntermediate)
        {
            return;
        }

        var lineHeight = GetLineHeight();

        if (lineHeight <= 0)
        {
            return;
        }

        var aligned = Math.Round(ScrollViewer.VerticalOffset / lineHeight) * lineHeight;

        if (Math.Abs(aligned - ScrollViewer.VerticalOffset) > 0.5)
        {
            ScrollViewer.ChangeView(null, aligned, null, true);
        }
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

    private static void OnVirtualizationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HexViewControl)d).UpdateVirtualization();
}
