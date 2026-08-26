using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Models.Query.Trace.Columnstore;
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

    public ColumnstoreScanControl()
    {
        InitializeComponent();

        ScanCanvas.PaintSurface += OnPaintSurface;

        ActualThemeChanged += (_, _) => ScanCanvas.Invalidate();
    }

    public void Refresh() => ScanCanvas.Invalidate();

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ColumnstoreScanControl)d).ScanCanvas.Invalidate();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var bounds = new SKRect(0, 0, e.Info.Width, e.Info.Height);

        var colour = new SKColor(NodeColour.R, NodeColour.G, NodeColour.B, NodeColour.A);

        ColumnstoreScanRenderer.Draw(e.Surface.Canvas,
                                     bounds,
                                     RowGroups ?? [],
                                     ActiveRowGroupId,
                                     BatchFirstRow,
                                     BatchRowCount,
                                     colour,
                                     ActualTheme == ElementTheme.Dark);
    }
}
