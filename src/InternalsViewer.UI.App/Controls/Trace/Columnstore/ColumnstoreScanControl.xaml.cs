using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Query.Trace.Columnstore;
using InternalsViewer.UI.App.Controls.Columnstore;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Trace.Columnstore;

public sealed partial class ColumnstoreScanControl
{
    public static readonly DependencyProperty RowGroupsProperty
        = DependencyProperty.Register(nameof(RowGroups),
                                      typeof(IReadOnlyList<ScanRowGroup>),
                                      typeof(ColumnstoreScanControl),
                                      new PropertyMetadata(null, OnVisualChanged));

    public IReadOnlyList<ScanRowGroup>? RowGroups
    {
        get => (IReadOnlyList<ScanRowGroup>?)GetValue(RowGroupsProperty);
        set => SetValue(RowGroupsProperty, value);
    }

    public static readonly DependencyProperty ActiveRowGroupIdProperty
        = DependencyProperty.Register(nameof(ActiveRowGroupId),
                                      typeof(int?),
                                      typeof(ColumnstoreScanControl),
                                      new PropertyMetadata(null, OnVisualChanged));

    public int? ActiveRowGroupId
    {
        get => (int?)GetValue(ActiveRowGroupIdProperty);
        set => SetValue(ActiveRowGroupIdProperty, value);
    }

    public static readonly DependencyProperty ScanVersionProperty
        = DependencyProperty.Register(nameof(ScanVersion),
                                      typeof(int),
                                      typeof(ColumnstoreScanControl),
                                      new PropertyMetadata(0, OnVisualChanged));

    public int ScanVersion
    {
        get => (int)GetValue(ScanVersionProperty);
        set => SetValue(ScanVersionProperty, value);
    }

    public static readonly DependencyProperty BatchFirstRowProperty
        = DependencyProperty.Register(nameof(BatchFirstRow),
                                      typeof(int),
                                      typeof(ColumnstoreScanControl),
                                      new PropertyMetadata(0, OnVisualChanged));

    public int BatchFirstRow
    {
        get => (int)GetValue(BatchFirstRowProperty);
        set => SetValue(BatchFirstRowProperty, value);
    }

    public static readonly DependencyProperty BatchRowCountProperty
        = DependencyProperty.Register(nameof(BatchRowCount),
                                      typeof(int),
                                      typeof(ColumnstoreScanControl),
                                      new PropertyMetadata(0, OnVisualChanged));

    public int BatchRowCount
    {
        get => (int)GetValue(BatchRowCountProperty);
        set => SetValue(BatchRowCountProperty, value);
    }

    public static readonly DependencyProperty NodeColourProperty
        = DependencyProperty.Register(nameof(NodeColour),
                                      typeof(Windows.UI.Color),
                                      typeof(ColumnstoreScanControl),
                                      new PropertyMetadata(Microsoft.UI.Colors.SteelBlue, OnVisualChanged));

    public Windows.UI.Color NodeColour
    {
        get => (Windows.UI.Color)GetValue(NodeColourProperty);
        set => SetValue(NodeColourProperty, value);
    }

    // Pointer movement (px) that turns a press into a pan.
    private const double DragThreshold = 3;

    private readonly ColumnstoreScanRenderer _renderer = new();

    private readonly CanvasViewport _viewport = new();

    private List<ColumnstoreRegion> _regions = [];

    private bool _isPointerDown;

    private bool _isDragging;

    private global::Windows.Foundation.Point _dragStart;

    private float _dragStartOffsetX;

    private float _dragStartOffsetY;

    public ColumnstoreScanControl()
    {
        InitializeComponent();

        ScanCanvas.PaintSurface += OnPaintSurface;

        ScanCanvas.PointerPressed += OnPointerPressed;

        ScanCanvas.PointerReleased += OnPointerReleased;

        ScanCanvas.PointerCaptureLost += OnPointerReleased;

        ScanCanvas.PointerMoved += OnPointerMoved;

        ScanCanvas.PointerWheelChanged += OnPointerWheelChanged;

        ScanCanvas.PointerExited += OnPointerExited;

        ActualThemeChanged += (_, _) => ScanCanvas.Invalidate();
    }

    public void Refresh() => ScanCanvas.Invalidate();

    public void Dispose()
    {
        ScanCanvas.PaintSurface -= OnPaintSurface;

        ScanCanvas.PointerPressed -= OnPointerPressed;

        ScanCanvas.PointerReleased -= OnPointerReleased;

        ScanCanvas.PointerCaptureLost -= OnPointerReleased;

        ScanCanvas.PointerMoved -= OnPointerMoved;

        ScanCanvas.PointerWheelChanged -= OnPointerWheelChanged;

        ScanCanvas.PointerExited -= OnPointerExited;

        _renderer.Dispose();
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ColumnstoreScanControl)d;

        control.UpdateExtent();

        control.ScanCanvas.Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var bounds = new SKRect(0, 0, e.Info.Width, e.Info.Height);

        var colour = new SKColor(NodeColour.R, NodeColour.G, NodeColour.B, NodeColour.A);

        var canvas = e.Surface.Canvas;

        canvas.Save();

        _viewport.Apply(canvas);

        _regions = _renderer.Draw(canvas,
                                     bounds,
                                     RowGroups ?? [],
                                     ActiveRowGroupId,
                                     BatchFirstRow,
                                     BatchRowCount,
                                     colour,
                                     ActualTheme == ElementTheme.Dark);

        canvas.Restore();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = true;
        _isDragging = false;
        _dragStart = e.GetCurrentPoint(ScanCanvas).Position;
        _dragStartOffsetX = _viewport.OffsetX;
        _dragStartOffsetY = _viewport.OffsetY;

        ScanCanvas.CapturePointer(e.Pointer);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }

        _isPointerDown = false;
        _isDragging = false;

        ScanCanvas.ReleasePointerCapture(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(ScanCanvas).Position;

        if (_isPointerDown && Pan(position))
        {
            return;
        }

        Tooltip.Show(FindRegion((float)position.X, (float)position.Y), position);
    }

    /// <summary>
    /// Wheel scrolls, and with control held zooms about the pointer
    /// </summary>
    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(ScanCanvas);

        var delta = pointer.Properties.MouseWheelDelta;

        Tooltip.Hide();

        e.Handled = true;

        var changed = IsControlPressed()
            ? _viewport.ZoomAt(delta, pointer.Position.X, pointer.Position.Y, pointer.Timestamp)
            : _viewport.SetOffset(_viewport.OffsetX, _viewport.OffsetY - delta);

        if (changed)
        {
            SyncScrollBars();

            ScanCanvas.Invalidate();
        }
    }

    private static bool IsControlPressed()
        => InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

    /// <summary>
    /// Drags the drawing under the pointer, once the press has moved far enough to be a drag
    /// </summary>
    private bool Pan(global::Windows.Foundation.Point position)
    {
        var deltaX = position.X - _dragStart.X;
        var deltaY = position.Y - _dragStart.Y;

        if (!_isDragging && Math.Abs(deltaX) < DragThreshold && Math.Abs(deltaY) < DragThreshold)
        {
            return false;
        }

        if (!_isDragging)
        {
            _isDragging = true;

            Tooltip.Hide();

            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        }

        if (_viewport.SetOffset(_dragStartOffsetX - (float)deltaX, _dragStartOffsetY - (float)deltaY))
        {
            SyncScrollBars();

            ScanCanvas.Invalidate();
        }

        return true;
    }

    private void ScrollBar_OnScroll(object sender, ScrollEventArgs e)
    {
        var horizontal = ReferenceEquals(sender, HorizontalScrollBar) ? (float)e.NewValue : _viewport.OffsetX;
        var vertical = ReferenceEquals(sender, VerticalScrollBar) ? (float)e.NewValue : _viewport.OffsetY;

        if (_viewport.SetOffset(horizontal, vertical))
        {
            SyncScrollBars();

            ScanCanvas.Invalidate();
        }
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateExtent();

        ScanCanvas.Invalidate();
    }

    /// <summary>
    /// The drawing fits the canvas until the row groups hit their smallest height, past which it is scrolled
    /// </summary>
    private void UpdateExtent()
    {
        var viewportWidth = (float)ScanCanvas.ActualWidth;
        var viewportHeight = (float)ScanCanvas.ActualHeight;

        var content = ColumnstoreScanRenderer.GetContentHeight(RowGroups?.Count ?? 0, viewportHeight);

        _viewport.SetExtent(viewportWidth, content, viewportWidth, viewportHeight);

        SyncScrollBars();
    }

    private void SyncScrollBars()
    {
        VerticalScrollBar.Maximum = _viewport.MaximumOffsetY;
        VerticalScrollBar.ViewportSize = ScanCanvas.ActualHeight;
        VerticalScrollBar.Visibility = _viewport.MaximumOffsetY > 0 ? Visibility.Visible : Visibility.Collapsed;
        VerticalScrollBar.Value = _viewport.OffsetY;

        HorizontalScrollBar.Maximum = _viewport.MaximumOffsetX;
        HorizontalScrollBar.ViewportSize = ScanCanvas.ActualWidth;
        HorizontalScrollBar.Visibility = _viewport.MaximumOffsetX > 0 ? Visibility.Visible : Visibility.Collapsed;
        HorizontalScrollBar.Value = _viewport.OffsetX;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => Tooltip.Hide();

    private ColumnstoreRegion? FindRegion(float x, float y)
    {
        var point = _viewport.ToContent(x, y);

        return _regions.Find(r => r.Bounds.Contains(point));
    }
}
