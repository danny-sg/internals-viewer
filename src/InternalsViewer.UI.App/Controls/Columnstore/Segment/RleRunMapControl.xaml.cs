using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Columnstore.Segment;

/// <summary>
/// The RLE array drawn as two tracks of runs, which is the shape of the array rather than its entries
/// </summary>
public sealed partial class RleRunMapControl : IDisposable
{
    private const int MinimumRowSpan = 16;

    public static readonly DependencyProperty RunsProperty
        = DependencyProperty.Register(nameof(Runs),
                                      typeof(IReadOnlyList<RleRunDetail>),
                                      typeof(RleRunMapControl),
                                      new PropertyMetadata(null, OnRunsChanged));

    public IReadOnlyList<RleRunDetail>? Runs
    {
        get => (IReadOnlyList<RleRunDetail>?)GetValue(RunsProperty);
        set => SetValue(RunsProperty, value);
    }

    public static readonly DependencyProperty ValueLabelProperty
        = DependencyProperty.Register(nameof(ValueLabel),
                                      typeof(string),
                                      typeof(RleRunMapControl),
                                      new PropertyMetadata("Value", OnValueLabelChanged));

    /// <summary>
    /// What the first track holds, being the runs that stand for a single value
    /// </summary>
    public string ValueLabel
    {
        get => (string)GetValue(ValueLabelProperty);
        set => SetValue(ValueLabelProperty, value);
    }

    public static readonly DependencyProperty IndexLabelProperty
        = DependencyProperty.Register(nameof(IndexLabel),
                                      typeof(string),
                                      typeof(RleRunMapControl),
                                      new PropertyMetadata("Bit Pack", OnIndexLabelChanged));

    /// <summary>
    /// What the second track holds, being the runs that cover a sequence rather than a single value
    /// </summary>
    public string IndexLabel
    {
        get => (string)GetValue(IndexLabelProperty);
        set => SetValue(IndexLabelProperty, value);
    }

    private readonly RleRunMapRenderer _renderer = new();

    private int _hoveredIndex = -1;

    private int _firstRow;

    private int _rowSpan;

    private bool _isThemeDirty = true;

    public RleRunMapControl()
    {
        InitializeComponent();

        MapCanvas.PaintSurface += OnPaintSurface;
        MapCanvas.PointerPressed += OnPointerPressed;
        MapCanvas.PointerMoved += OnPointerMoved;
        MapCanvas.PointerExited += OnPointerExited;
        MapCanvas.PointerWheelChanged += OnPointerWheelChanged;

        ActualThemeChanged += OnActualThemeChanged;
    }

    public event EventHandler<SegmentNavigationTarget>? RunInvoked;

    public void Dispose()
    {
        MapCanvas.PaintSurface -= OnPaintSurface;
        MapCanvas.PointerPressed -= OnPointerPressed;
        MapCanvas.PointerMoved -= OnPointerMoved;
        MapCanvas.PointerExited -= OnPointerExited;
        MapCanvas.PointerWheelChanged -= OnPointerWheelChanged;

        ActualThemeChanged -= OnActualThemeChanged;

        _renderer.Dispose();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        _isThemeDirty = true;

        MapCanvas.Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        e.Surface.Canvas.Clear(SKColors.Transparent);

        if (Runs is not { Count: > 0 } runs)
        {
            return;
        }

        if (_isThemeDirty)
        {
            var isDark = ActualTheme == ElementTheme.Dark;

            _renderer.TrackColour = isDark ? ColumnstoreColours.DarkPanel : ColumnstoreColours.Panel;
            _renderer.LabelColour = isDark ? ColumnstoreColours.DarkMuted : ColumnstoreColours.Muted;

            _isThemeDirty = false;
        }

        _renderer.Draw(e.Surface.Canvas, runs, (float)MapCanvas.ActualWidth, _firstRow, _rowSpan);
    }

    /// <summary>
    /// Zooms about the row under the pointer, so what is being looked at stays where it is
    /// </summary>
    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (Runs is not { Count: > 0 } runs)
        {
            return;
        }

        var total = RleRunMapRenderer.TotalRows(runs);

        var point = e.GetCurrentPoint(MapCanvas);

        var trackWidth = (float)MapCanvas.ActualWidth - RleRunMapRenderer.GutterWidth;

        var fraction = trackWidth <= 0
            ? 0.5
            : Math.Clamp(((float)point.Position.X - RleRunMapRenderer.GutterWidth) / trackWidth, 0, 1);

        var anchor = _firstRow + (fraction * _rowSpan);

        var factor = point.Properties.MouseWheelDelta > 0 ? 1 / 1.25 : 1.25;

        _rowSpan = (int)Math.Clamp(_rowSpan * factor, Math.Min(MinimumRowSpan, total), total);

        _firstRow = (int)Math.Clamp(anchor - (fraction * _rowSpan), 0, Math.Max(0, total - _rowSpan));

        TooltipPopup.IsOpen = false;

        _hoveredIndex = -1;

        UpdateScrollBar();

        MapCanvas.Invalidate();

        e.Handled = true;
    }

    /// <summary>
    /// Names the run under the pointer, the tracks being too dense for a label per run
    /// </summary>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (Runs is not { Count: > 0 } runs)
        {
            return;
        }

        var position = e.GetCurrentPoint(MapCanvas).Position;

        var index = RleRunMapRenderer.GetRunIndex(runs,
                                                  (float)position.X,
                                                  (float)position.Y,
                                                  (float)MapCanvas.ActualWidth,
                                                  _firstRow,
                                                  _rowSpan);

        if (index != _hoveredIndex)
        {
            _hoveredIndex = index;

            if (index < 0)
            {
                TooltipPopup.IsOpen = false;
            }
            else
            {
                var run = runs[index];

                var row = RleRunMapRenderer.GetRow((float)position.X,
                                                   (float)MapCanvas.ActualWidth,
                                                   _firstRow,
                                                   _rowSpan);

                TooltipText.Text = $"Row {row}{Environment.NewLine}"
                                   + $"{(run.IsPureValue ? ValueLabel : IndexLabel)} {run.ValueDescription}{Environment.NewLine}"
                                   + $"Run {run.Index}, count {run.Count}";

                TooltipPopup.IsOpen = true;
            }
        }

        if (!TooltipPopup.IsOpen)
        {
            return;
        }

        var relative = e.GetCurrentPoint(this).Position;

        TooltipPopup.HorizontalOffset = relative.X + 16;
        TooltipPopup.VerticalOffset = relative.Y + 16;
    }

    /// <summary>
    /// Shows the scroll bar only while zoomed in, there being nothing to scroll past at full span
    /// </summary>
    private void UpdateScrollBar()
    {
        var total = RleRunMapRenderer.TotalRows(Runs ?? []);

        var maximum = Math.Max(0, total - _rowSpan);

        HorizontalScrollBar.Maximum = maximum;
        HorizontalScrollBar.ViewportSize = _rowSpan;
        HorizontalScrollBar.SmallChange = Math.Max(1, _rowSpan / 8);
        HorizontalScrollBar.LargeChange = Math.Max(1, _rowSpan);
        HorizontalScrollBar.Value = Math.Clamp(_firstRow, 0, maximum);

        HorizontalScrollBar.Visibility = maximum > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ScrollBar_OnScroll(object sender, ScrollEventArgs e)
    {
        _firstRow = (int)e.NewValue;

        TooltipPopup.IsOpen = false;

        _hoveredIndex = -1;

        MapCanvas.Invalidate();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hoveredIndex = -1;

        TooltipPopup.IsOpen = false;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Runs is not { Count: > 0 } runs)
        {
            return;
        }

        var position = e.GetCurrentPoint(MapCanvas).Position;

        var index = RleRunMapRenderer.GetRunIndex(runs,
                                                  (float)position.X,
                                                  (float)position.Y,
                                                  (float)MapCanvas.ActualWidth,
                                                  _firstRow,
                                                  _rowSpan);

        if (index < 0)
        {
            return;
        }

        _renderer.SelectedIndex = index;

        _renderer.SelectedRow = RleRunMapRenderer.GetRow((float)position.X,
                                                         (float)MapCanvas.ActualWidth,
                                                         _firstRow,
                                                         _rowSpan);

        MapCanvas.Invalidate();

        RunInvoked?.Invoke(this, new SegmentNavigationTarget(SegmentRegion.RleArray, runs[index].Offset));
    }

    private static void OnValueLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RleRunMapControl)d;

        control._renderer.ValueLabel = control.ValueLabel;

        control.MapCanvas.Invalidate();
    }

    private static void OnIndexLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RleRunMapControl)d;

        control._renderer.IndexLabel = control.IndexLabel;

        control.MapCanvas.Invalidate();
    }

    private static void OnRunsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RleRunMapControl)d;

        control._renderer.SelectedIndex = -1;

        control._renderer.SelectedRow = -1;

        control._hoveredIndex = -1;

        control.TooltipPopup.IsOpen = false;

        control._firstRow = 0;

        control._rowSpan = RleRunMapRenderer.TotalRows(control.Runs ?? []);

        control.UpdateScrollBar();

        control.MapCanvas.Invalidate();
    }
}
