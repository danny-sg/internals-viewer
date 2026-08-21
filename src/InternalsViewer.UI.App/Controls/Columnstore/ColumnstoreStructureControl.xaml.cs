using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.UI.App.Models.Columnstore;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
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

    /// <summary>
    /// Coding of each dictionary's pages, which lands after the drawing has already been painted
    /// </summary>
    /// <remarks>
    /// Held as object because the dependency property system carries it into the XAML type table, which a generic
    /// dictionary of its own is awkward in. It changes the drawing without moving it, so the scroll is left alone.
    /// </remarks>
    public object? DictionaryCoding
    {
        get => GetValue(DictionaryCodingProperty);
        set => SetValue(DictionaryCodingProperty, value);
    }

    public static readonly DependencyProperty DictionaryCodingProperty
        = DependencyProperty.Register(nameof(DictionaryCoding),
                                      typeof(object),
                                      typeof(ColumnstoreStructureControl),
                                      new PropertyMetadata(null, OnCodingChanged));

    /// <summary>
    /// Repaints without moving, for detail that arrives after the drawing was laid out
    /// </summary>
    public int Revision
    {
        get => (int)GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public static readonly DependencyProperty RevisionProperty
        = DependencyProperty.Register(nameof(Revision),
                                      typeof(int),
                                      typeof(ColumnstoreStructureControl),
                                      new PropertyMetadata(0, OnRevisionChanged));

    private static void OnRevisionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ColumnstoreStructureControl)d).StructureCanvas.Invalidate();

    private static void OnCodingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ColumnstoreStructureControl)d;

        control._renderer.DictionaryCoding = e.NewValue as IReadOnlyDictionary<long, SubLobType>
                                             ?? new Dictionary<long, SubLobType>();

        control.StructureCanvas.Invalidate();
    }

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

        _renderer.HoverColour = isDark ? ColumnstoreColours.DarkHover : ColumnstoreColours.Selection;
        _renderer.TextColour = isDark ? ColumnstoreColours.DarkText : ColumnstoreColours.Text;
        _renderer.MutedColour = isDark ? ColumnstoreColours.DarkMuted : ColumnstoreColours.Muted;
        _renderer.PanelColour = ProbeColour(PanelProbe, isDark ? ColumnstoreColours.DarkPanel : ColumnstoreColours.Panel);
        _renderer.BandColour = ProbeColour(BandProbe, _renderer.PanelColour);
        _renderer.HoverBandColour = ProbeColour(HoverBandProbe, _renderer.BandColour);
        _renderer.KeywordColour = ProbeColour(KeywordProbe, _renderer.TextColour);
        _renderer.NumberColour = ProbeColour(NumberProbe, _renderer.TextColour);
        _renderer.PunctuationColour = ProbeColour(PunctuationProbe, _renderer.MutedColour);
    }

    /// <summary>
    /// Reads a theme brush off a zero sized element, which is what keeps the drawing in step with the theme
    /// </summary>
    /// <remarks>
    /// Resolved through an element rather than the application resources so it follows this control's actual theme,
    /// which is what changes when the theme is switched under it.
    /// </remarks>
    private static SKColor ProbeColour(Border probe, SKColor fallback)
        => probe.Background is SolidColorBrush brush
            ? new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A)
            : fallback;

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

        var columnId = ColumnAt((float)point.X, (float)point.Y);

        var changed = !ReferenceEquals(region, _renderer.Hover) || columnId != _renderer.HoveredColumnId;

        _renderer.Hover = region;

        _renderer.HoveredColumnId = columnId;

        if (!changed)
        {
            return;
        }

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

    /// <summary>
    /// Whether any row group carries a local dictionary, which is what the row of them costs its height for
    /// </summary>
    private bool HasLocalDictionaries()
        => Index?.RowGroups.Any(r => r.Segments.Any(s => s.LocalDictionary is not null)) ?? false;

    /// <summary>
    /// Column under the pointer, which the bands take from the pointer rather than from what it is over
    /// </summary>
    private int ColumnAt(float x, float y)
    {
        if (Index is not { } index)
        {
            return -1;
        }

        // The bands are drawn scrolled, so the pointer is put back into the coordinates they were laid out in
        var canvasY = y + _scrollOffset;

        if (canvasY < _renderer.BandTop || canvasY > _renderer.BandBottom)
        {
            return -1;
        }

        var slot = ColumnstoreLayout.GetColumnIndex(x, (float)StructureCanvas.ActualWidth, index.Columns.Count);

        return slot < 0 ? -1 : index.Columns[slot].ColumnStoreColumnId;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _renderer.Hover = null;

        _renderer.HoveredColumnId = -1;

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

        var columnWidth = Index is { } columns
            ? ColumnstoreLayout.GetColumnWidth((float)StructureCanvas.ActualWidth, columns.Columns.Count)
            : 0;

        var content = ColumnstoreLayout.GetContentHeight(RowGroups?.Count ?? 0,
                                                         headerHeight,
                                                         HasLocalDictionaries(),
                                                         columnWidth);

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
