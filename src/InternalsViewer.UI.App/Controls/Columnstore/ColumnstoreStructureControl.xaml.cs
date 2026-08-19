using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Columnstore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Columnstore;

public sealed partial class ColumnstoreStructureControl : IDisposable
{
    private readonly ColumnstoreStructureRenderer _renderer = new();

    private List<ColumnstoreRegion> _regions = [];

    private float _scrollOffset;

    public event EventHandler<ColumnstoreRegion>? ElementClicked;

    public ColumnstoreStructureControl()
    {
        InitializeComponent();

        StructureCanvas.PaintSurface += OnPaintSurface;
        StructureCanvas.PointerPressed += OnPointerPressed;
        StructureCanvas.PointerMoved += OnPointerMoved;
        StructureCanvas.PointerWheelChanged += OnPointerWheelChanged;
        StructureCanvas.PointerExited += OnPointerExited;

        ActualThemeChanged += OnActualThemeChanged;
    }

    public ColumnStoreIndex? Index
    {
        get => (ColumnStoreIndex?)GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    public static readonly DependencyProperty IndexProperty
        = DependencyProperty.Register(nameof(Index),
                                      typeof(ColumnStoreIndex),
                                      typeof(ColumnstoreStructureControl),
                                      new PropertyMetadata(null, OnSourceChanged));

    public IReadOnlyList<RowGroupSummary>? RowGroups
    {
        get => (IReadOnlyList<RowGroupSummary>?)GetValue(RowGroupsProperty);
        set => SetValue(RowGroupsProperty, value);
    }

    public static readonly DependencyProperty RowGroupsProperty
        = DependencyProperty.Register(nameof(RowGroups),
                                      typeof(IReadOnlyList<RowGroupSummary>),
                                      typeof(ColumnstoreStructureControl),
                                      new PropertyMetadata(null, OnSourceChanged));

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ColumnstoreStructureControl)d;

        control._scrollOffset = 0;
        control.UpdateScrollBar();
        control.StructureCanvas.Invalidate();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args) => StructureCanvas.Invalidate();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        e.Surface.Canvas.Clear(SKColors.Transparent);

        if (Index is not { } index || RowGroups is not { Count: > 0 } rowGroups)
        {
            return;
        }

        ApplyTheme();

        _regions = _renderer.Draw(e.Surface.Canvas,
                                  index,
                                  rowGroups,
                                  (float)StructureCanvas.ActualWidth,
                                  _scrollOffset);
    }

    private void ApplyTheme()
    {
        var isDark = ActualTheme == ElementTheme.Dark;

        _renderer.HoverColour = isDark ? new SKColor(0x85, 0xB7, 0xEB) : new SKColor(0x18, 0x5F, 0xA5);
        _renderer.TextColour = isDark ? new SKColor(0xEC, 0xEB, 0xE6) : new SKColor(0x20, 0x20, 0x20);
        _renderer.MutedColour = isDark ? new SKColor(0x9A, 0x98, 0x92) : new SKColor(0x70, 0x70, 0x70);
        _renderer.PanelColour = isDark ? new SKColor(0x2A, 0x2A, 0x28) : new SKColor(0xF4, 0xF3, 0xEF);
        _renderer.BorderColour = isDark ? new SKColor(0x44, 0x44, 0x41) : new SKColor(0xD0, 0xCE, 0xC6);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(StructureCanvas).Position;

        if (FindRegion((float)point.X, (float)point.Y) is not { } region)
        {
            return;
        }

        _renderer.Selected = region;

        StructureCanvas.Invalidate();

        if (region.ElementType != ColumnstoreElementType.RowGroup)
        {
            ElementClicked?.Invoke(this, region);
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(StructureCanvas).Position;

        var region = FindRegion((float)point.X, (float)point.Y);

        if (ReferenceEquals(region, _renderer.Hover))
        {
            return;
        }

        _renderer.Hover = region;

        StructureCanvas.Invalidate();

        ShowTooltip(region, e.GetCurrentPoint(this).Position);
    }

    /// <summary>
    /// A popup rather than a plain tooltip, so a region can show a set of fields and not just one line
    /// </summary>
    private void ShowTooltip(ColumnstoreRegion? region, global::Windows.Foundation.Point position)
    {
        if (region is null || region.Details.Count == 0)
        {
            TooltipPopup.IsOpen = false;

            return;
        }

        TooltipTitle.Text = region.Label;
        TooltipDetails.ItemsSource = region.Details;

        TooltipPopup.HorizontalOffset = position.X + 12;
        TooltipPopup.VerticalOffset = position.Y + 12;

        TooltipPopup.IsOpen = true;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _renderer.Hover = null;

        TooltipPopup.IsOpen = false;

        StructureCanvas.Invalidate();
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(StructureCanvas).Properties.MouseWheelDelta;

        TooltipPopup.IsOpen = false;

        SetScrollOffset(_scrollOffset - delta);

        e.Handled = true;
    }

    private void ScrollBar_OnScroll(object sender, ScrollEventArgs e) => SetScrollOffset((float)e.NewValue);

    private void SetScrollOffset(float value)
    {
        var clamped = Math.Clamp(value, 0, (float)VerticalScrollBar.Maximum);

        if (Math.Abs(clamped - _scrollOffset) < 0.5f)
        {
            return;
        }

        _scrollOffset = clamped;

        VerticalScrollBar.Value = clamped;

        StructureCanvas.Invalidate();
    }

    /// <summary>
    /// Regions are recorded in draw order, so the last match is the innermost one a click landed on
    /// </summary>
    private ColumnstoreRegion? FindRegion(float x, float y)
    {
        var point = new SKPoint(x, y + _scrollOffset);

        for (var i = _regions.Count - 1; i >= 0; i--)
        {
            if (_regions[i].Bounds.Contains(point))
            {
                return _regions[i];
            }
        }

        return null;
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScrollBar();

        StructureCanvas.Invalidate();
    }

    private void UpdateScrollBar()
    {
        var headerHeight = Index is { } index
            ? ColumnstoreLayout.GetHeaderHeight(index.DeleteBitmap is not null,
                                                index.Columns.Count(c => c.GlobalDictionary is not null))
            : 0;

        var content = ColumnstoreLayout.GetContentHeight(RowGroups?.Count ?? 0, headerHeight);

        var viewport = (float)StructureCanvas.ActualHeight;

        var maximum = Math.Max(0, content - viewport);

        VerticalScrollBar.Maximum = maximum;
        VerticalScrollBar.ViewportSize = viewport;
        VerticalScrollBar.Visibility = maximum > 0 ? Visibility.Visible : Visibility.Collapsed;

        _scrollOffset = Math.Min(_scrollOffset, maximum);
        VerticalScrollBar.Value = _scrollOffset;
    }

    public void Dispose()
    {
        StructureCanvas.PaintSurface -= OnPaintSurface;
        StructureCanvas.PointerPressed -= OnPointerPressed;
        StructureCanvas.PointerMoved -= OnPointerMoved;
        StructureCanvas.PointerWheelChanged -= OnPointerWheelChanged;
        StructureCanvas.PointerExited -= OnPointerExited;

        ActualThemeChanged -= OnActualThemeChanged;

        _renderer.Dispose();
    }
}
