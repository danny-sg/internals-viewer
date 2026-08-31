using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.UI.App.Models.Columnstore;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Columnstore.Structure;

public sealed partial class ColumnstoreStructureControl : IDisposable
{
    public static readonly DependencyProperty IndexProperty
        = DependencyProperty.Register(nameof(Index),
                                      typeof(ColumnStoreIndex),
                                      typeof(ColumnstoreStructureControl),
                                      new PropertyMetadata(null, OnSourceChanged));

    public ColumnStoreIndex? Index
    {
        get => (ColumnStoreIndex?)GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    public static readonly DependencyProperty RowGroupsProperty
        = DependencyProperty.Register(nameof(RowGroups),
                                      typeof(IReadOnlyList<RowGroupSummary>),
                                      typeof(ColumnstoreStructureControl),
                                      new PropertyMetadata(null, OnSourceChanged));

    public IReadOnlyList<RowGroupSummary>? RowGroups
    {
        get => (IReadOnlyList<RowGroupSummary>?)GetValue(RowGroupsProperty);
        set => SetValue(RowGroupsProperty, value);
    }

    public static readonly DependencyProperty DictionaryCodingProperty
        = DependencyProperty.Register(nameof(DictionaryCoding),
                                      typeof(object),
                                      typeof(ColumnstoreStructureControl),
                                      new PropertyMetadata(null, OnCodingChanged));

    public object? DictionaryCoding
    {
        get => GetValue(DictionaryCodingProperty);
        set => SetValue(DictionaryCodingProperty, value);
    }

    public static readonly DependencyProperty RevisionProperty
        = DependencyProperty.Register(nameof(Revision),
                                      typeof(int),
                                      typeof(ColumnstoreStructureControl),
                                      new PropertyMetadata(0, OnRevisionChanged));

    /// <summary>
    /// Repaints without moving, for detail that arrives after the drawing was laid out
    /// </summary>
    public int Revision
    {
        get => (int)GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public static readonly DependencyProperty DatabaseIdProperty
        = DependencyProperty.Register(nameof(DatabaseId),
                                      typeof(short),
                                      typeof(ColumnstoreStructureControl),
                                      new PropertyMetadata((short)0));

    /// <summary>
    /// The database the drawing is of, which CSINDEX needs as a literal
    /// </summary>
    public short DatabaseId
    {
        get => (short)GetValue(DatabaseIdProperty);
        set => SetValue(DatabaseIdProperty, value);
    }

    // Pointer movement (px) that turns a press into a pan rather than a click.
    private const double DragThreshold = 3;

    private readonly ColumnstoreStructureRenderer _renderer = new();

    private readonly CanvasViewport _viewport = new();

    private List<ColumnstoreRegion> _regions = [];

    private bool _isPointerDown;

    private bool _isDragging;

    private global::Windows.Foundation.Point _dragStart;

    private float _dragStartOffsetX;

    private float _dragStartOffsetY;

    private bool _isThemeDirty = true;

    private bool? _hasLocalDictionaries;

    public ColumnstoreStructureControl()
    {
        InitializeComponent();

        StructureCanvas.PaintSurface += OnPaintSurface;
        StructureCanvas.PointerPressed += OnPointerPressed;
        StructureCanvas.PointerReleased += OnPointerReleased;
        StructureCanvas.PointerCaptureLost += OnPointerReleased;
        StructureCanvas.PointerMoved += OnPointerMoved;
        StructureCanvas.PointerWheelChanged += OnPointerWheelChanged;
        StructureCanvas.PointerExited += OnPointerExited;
        StructureCanvas.RightTapped += OnRightTapped;

        ActualThemeChanged += OnActualThemeChanged;

        Loaded += OnLoaded;
    }

    public event EventHandler<ColumnstoreRegion>? ElementClicked;

    public void Dispose()
    {
        StructureCanvas.PaintSurface -= OnPaintSurface;
        StructureCanvas.PointerPressed -= OnPointerPressed;
        StructureCanvas.PointerReleased -= OnPointerReleased;
        StructureCanvas.PointerCaptureLost -= OnPointerReleased;
        StructureCanvas.PointerMoved -= OnPointerMoved;
        StructureCanvas.PointerWheelChanged -= OnPointerWheelChanged;
        StructureCanvas.PointerExited -= OnPointerExited;
        StructureCanvas.RightTapped -= OnRightTapped;

        ActualThemeChanged -= OnActualThemeChanged;

        Loaded -= OnLoaded;

        _renderer.Dispose();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        _isThemeDirty = true;

        StructureCanvas.Invalidate();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isThemeDirty = true;

        StructureCanvas.Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        e.Surface.Canvas.Clear(SKColors.Transparent);

        if (Index is not { } index || RowGroups is not { Count: > 0 } rowGroups)
        {
            return;
        }

        if (_isThemeDirty)
        {
            ApplyTheme();

            _isThemeDirty = false;
        }

        var canvas = e.Surface.Canvas;

        canvas.Save();

        _viewport.Apply(canvas);

        _regions = _renderer.Draw(canvas, index, rowGroups, (float)StructureCanvas.ActualWidth);

        canvas.Restore();
    }

    private void ApplyTheme()
    {
        var isDark = ActualTheme == ElementTheme.Dark;

        _renderer.TextColour = isDark ? ColumnstoreColours.DarkText : ColumnstoreColours.Text;
        _renderer.MutedColour = isDark ? ColumnstoreColours.DarkMuted : ColumnstoreColours.Muted;
        _renderer.PanelColour = ProbeColour(PanelProbe, isDark ? ColumnstoreColours.DarkPanel : ColumnstoreColours.Panel);
        _renderer.BandColour = ProbeColour(BandProbe, _renderer.PanelColour);
        _renderer.LocatorBandColour = isDark ? ColumnstoreColours.DarkLocatorBand : ColumnstoreColours.LocatorBand;
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
        var pointer = e.GetCurrentPoint(StructureCanvas);

        // Selecting is the left button's job, the right one opening the menu instead of moving the selection
        if (pointer.Properties.IsRightButtonPressed)
        {
            return;
        }

        _isPointerDown = true;
        _isDragging = false;
        _dragStart = pointer.Position;
        _dragStartOffsetX = _viewport.OffsetX;
        _dragStartOffsetY = _viewport.OffsetY;

        StructureCanvas.CapturePointer(e.Pointer);
    }

    /// <summary>
    /// A press that panned is the pan, one that did not is a click on whatever it landed on
    /// </summary>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isPointerDown && !_isDragging)
        {
            var point = e.GetCurrentPoint(StructureCanvas).Position;

            if (FindRegion((float)point.X, (float)point.Y) is { } region
                && region.ElementType != ColumnstoreElementType.RowGroup)
            {
                ElementClicked?.Invoke(this, region);
            }
        }

        if (_isDragging)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }

        _isPointerDown = false;
        _isDragging = false;

        StructureCanvas.ReleasePointerCapture(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(StructureCanvas).Position;

        if (_isPointerDown && Pan(point))
        {
            return;
        }

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
        Tooltip.Show(region, position);
    }

    /// <summary>
    /// Whether any row group carries a local dictionary, which is what the row of them costs its height for
    /// </summary>
    private bool HasLocalDictionaries()
        => _hasLocalDictionaries ??= Index?.RowGroups.Any(r => r.Segments.Any(s => s.LocalDictionary is not null)) ?? false;

    /// <summary>
    /// Column under the pointer, which the bands take from the pointer rather than from what it is over
    /// </summary>
    private int ColumnAt(float x, float y)
    {
        if (Index is not { } index)
        {
            return -1;
        }

        // The bands are drawn through the viewport, so the pointer is put back into the coordinates they were laid out in
        var point = _viewport.ToContent(x, y);

        if (point.Y < _renderer.BandTop || point.Y > _renderer.BandBottom)
        {
            return -1;
        }

        var slot = ColumnstoreLayout.GetColumnIndex(point.X, (float)StructureCanvas.ActualWidth, index.Columns.Count);

        return slot < 0 ? -1 : index.Columns[slot].ColumnStoreColumnId;
    }

    /// <summary>
    /// Offers the CSINDEX command for whatever was right clicked, so what the drawing shows can be checked against
    /// what the engine reports
    /// </summary>
    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var point = e.GetPosition(StructureCanvas);

        if (FindRegion((float)point.X, (float)point.Y) is not { } region
            || !CsIndexCommand.CanBuild(region)
            || Index is not { } index)
        {
            return;
        }

        var flyout = CsIndexMenu.Build(region.Label,
                                       mode => CsIndexCommand.Build(region, DatabaseId, index.HobtId, mode));

        flyout?.ShowAt(StructureCanvas, point);
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _renderer.Hover = null;

        _renderer.HoveredColumnId = -1;

        Tooltip.Hide();

        StructureCanvas.Invalidate();
    }

    /// <summary>
    /// Wheel scrolls, and with control held zooms about the pointer
    /// </summary>
    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(StructureCanvas);

        var delta = pointer.Properties.MouseWheelDelta;

        Tooltip.Hide();

        e.Handled = true;

        var changed = IsControlPressed()
            ? _viewport.ZoomAt(delta, pointer.Position.X, pointer.Position.Y, pointer.Timestamp)
            : _viewport.SetOffset(_viewport.OffsetX, _viewport.OffsetY - delta);

        if (changed)
        {
            SyncScrollBars();

            StructureCanvas.Invalidate();
        }
    }

    private static bool IsControlPressed()
        => InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);

    /// <summary>
    /// Drags the drawing under the pointer, once the press has moved far enough to be a drag rather than a click
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

            StructureCanvas.Invalidate();
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

            StructureCanvas.Invalidate();
        }
    }

    /// <summary>
    /// Regions are recorded in draw order, so the last match is the innermost one a click landed on
    /// </summary>
    private ColumnstoreRegion? FindRegion(float x, float y)
    {
        var point = _viewport.ToContent(x, y);

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

        var columnHeaderHeight = Index is { } columns
            ? _renderer.GetColumnHeaderHeight(columns, (float)StructureCanvas.ActualWidth)
            : 0;

        var content = ColumnstoreLayout.GetContentHeight(RowGroups?.Count ?? 0,
                                                         headerHeight,
                                                         HasLocalDictionaries(),
                                                         columnHeaderHeight);

        // The drawing is laid out to the canvas width, so at a zoom of one there is nothing to pan horizontally
        _viewport.SetExtent((float)StructureCanvas.ActualWidth,
                            content,
                            (float)StructureCanvas.ActualWidth,
                            (float)StructureCanvas.ActualHeight);

        SyncScrollBars();
    }

    private void SyncScrollBars()
    {
        VerticalScrollBar.Maximum = _viewport.MaximumOffsetY;
        VerticalScrollBar.ViewportSize = StructureCanvas.ActualHeight;
        VerticalScrollBar.Visibility = _viewport.MaximumOffsetY > 0 ? Visibility.Visible : Visibility.Collapsed;
        VerticalScrollBar.Value = _viewport.OffsetY;

        HorizontalScrollBar.Maximum = _viewport.MaximumOffsetX;
        HorizontalScrollBar.ViewportSize = StructureCanvas.ActualWidth;
        HorizontalScrollBar.Visibility = _viewport.MaximumOffsetX > 0 ? Visibility.Visible : Visibility.Collapsed;
        HorizontalScrollBar.Value = _viewport.OffsetX;
    }

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

        control._viewport.SetOffset(0, 0);

        control._hasLocalDictionaries = null;

        control.UpdateScrollBar();

        control.StructureCanvas.Invalidate();
    }
}
