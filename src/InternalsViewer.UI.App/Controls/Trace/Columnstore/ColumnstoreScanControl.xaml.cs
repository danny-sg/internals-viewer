using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Query.Trace.Columnstore;
using InternalsViewer.UI.App.Controls.Columnstore;
using Microsoft.UI.Xaml.Input;
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

    private List<ColumnstoreRegion> _regions = [];

    public ColumnstoreScanControl()
    {
        InitializeComponent();

        ScanCanvas.PaintSurface += OnPaintSurface;

        ScanCanvas.PointerMoved += OnPointerMoved;

        ScanCanvas.PointerExited += OnPointerExited;

        ActualThemeChanged += (_, _) => ScanCanvas.Invalidate();
    }

    public void Refresh() => ScanCanvas.Invalidate();

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ColumnstoreScanControl)d).ScanCanvas.Invalidate();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var bounds = new SKRect(0, 0, e.Info.Width, e.Info.Height);

        var colour = new SKColor(NodeColour.R, NodeColour.G, NodeColour.B, NodeColour.A);

        _regions = ColumnstoreScanRenderer.Draw(e.Surface.Canvas,
                                     bounds,
                                     RowGroups ?? [],
                                     ActiveRowGroupId,
                                     BatchFirstRow,
                                     BatchRowCount,
                                     colour,
                                     ActualTheme == ElementTheme.Dark);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(ScanCanvas).Position;

        Tooltip.Show(FindRegion((float)position.X, (float)position.Y), position);
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => Tooltip.Hide();

    private ColumnstoreRegion? FindRegion(float x, float y)
        => _regions.Find(r => r.Bounds.Contains(x, y));
}
