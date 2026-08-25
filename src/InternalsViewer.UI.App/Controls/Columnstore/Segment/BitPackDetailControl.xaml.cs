using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Columnstore.Segment;

/// <summary>
/// The bits of one packed unit over the values read out of them
/// </summary>
public sealed partial class BitPackDetailControl : IDisposable
{
    public static readonly DependencyProperty UnitProperty
        = DependencyProperty.Register(nameof(Unit),
                                      typeof(BitpackUnitDetail),
                                      typeof(BitPackDetailControl),
                                      new PropertyMetadata(null, OnUnitChanged));

    public BitpackUnitDetail? Unit
    {
        get => (BitpackUnitDetail?)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    private readonly BitRulerRenderer _renderer = new();

    private List<(SKRect Bounds, int Index)> _regions = [];

    private bool _isThemeDirty = true;

    /// <summary>
    /// Names the bit under the pointer, the row being too fine to label every position along it
    /// </summary>
    private int _hoveredBit = -1;

    public BitPackDetailControl()
    {
        InitializeComponent();

        RulerCanvas.PaintSurface += OnPaintSurface;
        RulerCanvas.PointerPressed += OnPointerPressed;
        RulerCanvas.PointerMoved += OnPointerMoved;
        RulerCanvas.PointerExited += OnPointerExited;

        ActualThemeChanged += OnActualThemeChanged;
    }

    /// <summary>
    /// Releases the renderer, whose paints and fonts are native Skia handles the collector will not reclaim
    /// </summary>
    public void Dispose()
    {
        RulerCanvas.PaintSurface -= OnPaintSurface;
        RulerCanvas.PointerPressed -= OnPointerPressed;
        RulerCanvas.PointerMoved -= OnPointerMoved;
        RulerCanvas.PointerExited -= OnPointerExited;

        ActualThemeChanged -= OnActualThemeChanged;

        _renderer.Dispose();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        _isThemeDirty = true;

        RulerCanvas.Invalidate();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) => BitTooltip.IsOpen = false;

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        e.Surface.Canvas.Clear(SKColors.Transparent);

        if (Unit is not { } unit)
        {
            return;
        }

        if (_isThemeDirty)
        {
            ApplyTheme();

            _isThemeDirty = false;
        }

        _regions = _renderer.Draw(e.Surface.Canvas, unit, (float)RulerCanvas.ActualWidth);
    }

    private void ApplyTheme()
    {
        var isDark = ActualTheme == ElementTheme.Dark;

        _renderer.TextColour = isDark ? new SKColor(0xEC, 0xEB, 0xE6) : new SKColor(0x20, 0x20, 0x20);
        _renderer.MutedColour = isDark ? new SKColor(0x9A, 0x98, 0x92) : new SKColor(0x70, 0x70, 0x70);
        _renderer.BorderColour = isDark ? new SKColor(0x44, 0x44, 0x41) : new SKColor(0xD0, 0xCE, 0xC6);
        _renderer.PaddingColour = isDark ? new SKColor(0x33, 0x33, 0x31) : new SKColor(0xE8, 0xE7, 0xE2);
        _renderer.ValueBoxColour = isDark ? new SKColor(0x24, 0x24, 0x22) : new SKColor(0xFA, 0xF9, 0xF6);
        _renderer.ByteBoxColour = isDark ? new SKColor(0x30, 0x30, 0x2E) : new SKColor(0xF0, 0xEF, 0xEA);
        _renderer.ByteDividerColour = isDark ? new SKColor(0xD8, 0xD7, 0xD2) : SKColors.White;
        _renderer.SelectionColour = isDark ? new SKColor(0x85, 0xB7, 0xEB) : new SKColor(0x18, 0x5F, 0xA5);

        _renderer.BandColours = isDark
            ? [new SKColor(0x1D, 0x35, 0x4E), new SKColor(0x2A, 0x4A, 0x69)]
            : [new SKColor(0xD6, 0xE6, 0xF7), new SKColor(0xBB, 0xD6, 0xF0)];
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RulerCanvas).Position;

        foreach (var (bounds, index) in _regions)
        {
            if (!bounds.Contains((float)point.X, (float)point.Y))
            {
                continue;
            }

            ValueTable.SelectedItem = Unit?.Values.FirstOrDefault(v => v.Index == index);

            return;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RulerCanvas).Position;

        var bit = _renderer.GetBitAt((float)point.X, (float)point.Y);

        if (bit < 0)
        {
            _hoveredBit = -1;

            BitTooltip.IsOpen = false;

            return;
        }

        if (bit != _hoveredBit)
        {
            _hoveredBit = bit;

            BitTooltipText.Text = $"Bit {bit}";
        }

        BitTooltip.HorizontalOffset = point.X + 12;
        BitTooltip.VerticalOffset = point.Y + 18;

        BitTooltip.IsOpen = true;
    }

    private void ValueTable_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _renderer.SelectedValueIndex = (ValueTable.SelectedItem as BitpackValueDetail)?.Index ?? -1;

        RulerCanvas.Invalidate();
    }

    private static void OnUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (BitPackDetailControl)d;

        var unit = e.NewValue as BitpackUnitDetail;

        var visibility = unit is null ? Visibility.Collapsed : Visibility.Visible;

        control.EmptyText.Visibility = unit is null ? Visibility.Visible : Visibility.Collapsed;
        control.UnitText.Visibility = visibility;
        control.RulerCanvas.Visibility = visibility;
        control.ValueTable.Visibility = visibility;

        control.ValueTable.ItemsSource = unit?.Values;

        if (unit is not null)
        {
            control.UnitText.Text = $"Unit {unit.UnitIndex} at {unit.OffsetDescription}, "
                                    + $"{unit.Values.Count} values of {unit.EntrySizeBits} bits";
        }

        control.BitTooltip.IsOpen = false;

        control._renderer.SelectedValueIndex = -1;

        control.RulerCanvas.Invalidate();
    }
}
