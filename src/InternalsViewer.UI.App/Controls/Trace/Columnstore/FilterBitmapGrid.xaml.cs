using System;
using InternalsViewer.UI.App.Models.Query.Trace.Columnstore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Trace.Columnstore;

public sealed partial class FilterBitmapGrid : UserControl, IDisposable
{
    private const int Gap = 2;

    private const int MinCell = 4;

    private const int MaxCell = 16;

    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(nameof(Model),
                                    typeof(FilterBitmapModel),
                                    typeof(FilterBitmapGrid),
                                    new PropertyMetadata(null, OnModelChanged));

    public FilterBitmapGrid()
    {
        InitializeComponent();

        QualifyPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false, Color = new SKColor(86, 156, 214) };

        ExcludePaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false, Color = new SKColor(128, 128, 128, 60) };

        ToolTipService.SetToolTip(Canvas, Tip);

        Canvas.PaintSurface += OnPaint;

        Canvas.PointerMoved += OnPointerMoved;

        Canvas.PointerExited += OnPointerExited;

        Scroller.SizeChanged += OnScrollerSizeChanged;

        Loaded += OnLoaded;
    }

    public FilterBitmapModel? Model
    {
        get => (FilterBitmapModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private SKPaint QualifyPaint { get; }

    private SKPaint ExcludePaint { get; }

    private ToolTip Tip { get; } = new() { IsEnabled = false };

    private int CellSize { get; set; } = MinCell;

    private int Columns { get; set; } = 1;

    public void Dispose()
    {
        Canvas.PaintSurface -= OnPaint;

        Canvas.PointerMoved -= OnPointerMoved;

        Canvas.PointerExited -= OnPointerExited;

        Scroller.SizeChanged -= OnScrollerSizeChanged;

        Loaded -= OnLoaded;

        QualifyPaint.Dispose();

        ExcludePaint.Dispose();
    }

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FilterBitmapGrid grid)
        {
            grid.Bindings.Update();

            grid.Relayout();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Relayout();

    private void OnScrollerSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private void Relayout()
    {
        if (Model?.Cells is not { Count: > 0 } cells)
        {
            Canvas.Width = 0;

            Canvas.Height = 0;

            return;
        }

        var width = Scroller.ViewportWidth;

        var height = Scroller.ViewportHeight;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var count = cells.Count;

        var fits = false;

        var cellSize = MinCell;

        var columns = 1;

        for (var candidate = MaxCell; candidate >= MinCell; candidate--)
        {
            var pitch = candidate + Gap;

            var candidateColumns = Math.Max(1, (int)((width + Gap) / pitch));

            var rows = (count + candidateColumns - 1) / candidateColumns;

            if ((rows * pitch) - Gap <= height)
            {
                cellSize = candidate;

                columns = candidateColumns;

                fits = true;

                break;
            }
        }

        if (!fits)
        {
            const int pitch = MinCell + Gap;

            var usable = Math.Max(pitch, width - 14);

            columns = Math.Max(1, (int)((usable + Gap) / pitch));
        }

        CellSize = cellSize;

        Columns = columns;

        var totalRows = (count + columns - 1) / columns;

        Canvas.Width = width;

        Canvas.Height = fits ? height : (totalRows * (cellSize + Gap)) - Gap;

        Canvas.Invalidate();
    }

    private void OnPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        e.Surface.Canvas.Clear();

        if (Model?.Cells is not { Count: > 0 } cells)
        {
            return;
        }

        var pitch = CellSize + Gap;

        for (var i = 0; i < cells.Count; i++)
        {
            var x = (i % Columns) * pitch;

            var y = (i / Columns) * pitch;

            e.Surface.Canvas.DrawRect(x, y, CellSize, CellSize, cells[i].Qualifies ? QualifyPaint : ExcludePaint);
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (Model?.Cells is not { Count: > 0 } cells)
        {
            return;
        }

        var position = e.GetCurrentPoint(Canvas).Position;

        var pitch = CellSize + Gap;

        var column = (int)(position.X / pitch);

        var index = ((int)(position.Y / pitch) * Columns) + column;

        if (column >= Columns || index < 0 || index >= cells.Count)
        {
            Tip.IsEnabled = false;

            return;
        }

        var cell = cells[index];

        Tip.Content = $"Index {cell.Index}  ·  Data Id {cell.DataId}  ·  {cell.Value}";

        Tip.IsEnabled = true;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => Tip.IsEnabled = false;
}
