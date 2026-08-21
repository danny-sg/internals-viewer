using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// The bit walk one entry decodes through, wrapped across lines
/// </summary>
public sealed partial class HuffmanDecodeControl : IDisposable
{
    private readonly HuffmanDecodeRenderer _renderer = new();

    private List<(SKRect Bounds, int Index)> _regions = [];

    private float _scrollOffset;

    public HuffmanDecodeControl()
    {
        InitializeComponent();

        WalkCanvas.PaintSurface += OnPaintSurface;
        WalkCanvas.PointerPressed += OnPointerPressed;
        WalkCanvas.PointerWheelChanged += OnPointerWheelChanged;

        ActualThemeChanged += (_, _) => WalkCanvas.Invalidate();
    }

    public IReadOnlyList<HuffmanDecodeStep>? Steps
    {
        get => (IReadOnlyList<HuffmanDecodeStep>?)GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public static readonly DependencyProperty StepsProperty
        = DependencyProperty.Register(nameof(Steps),
                                      typeof(IReadOnlyList<HuffmanDecodeStep>),
                                      typeof(HuffmanDecodeControl),
                                      new PropertyMetadata(null, OnStepsChanged));

    private static void OnStepsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HuffmanDecodeControl)d;

        control._scrollOffset = 0;

        control.EmptyText.Visibility = e.NewValue is IReadOnlyList<HuffmanDecodeStep> { Count: > 0 }
            ? Visibility.Collapsed
            : Visibility.Visible;

        control.UpdateScrollBar();
        control.WalkCanvas.Invalidate();
    }

    public int SelectedStep
    {
        get => (int)GetValue(SelectedStepProperty);
        set => SetValue(SelectedStepProperty, value);
    }

    public static readonly DependencyProperty SelectedStepProperty
        = DependencyProperty.Register(nameof(SelectedStep),
                                      typeof(int),
                                      typeof(HuffmanDecodeControl),
                                      new PropertyMetadata(-1, OnSelectedStepChanged));

    private static void OnSelectedStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HuffmanDecodeControl)d;

        control._renderer.SelectedStep = (int)e.NewValue;

        control.WalkCanvas.Invalidate();
    }

    /// <summary>
    /// The coded stream the steps read from, which the bits and words are drawn out of
    /// </summary>
    /// <remarks>
    /// Not called Content, that being the child a user control already carries.
    /// </remarks>
    public ReadOnlyMemory<byte> CodedContent
    {
        get => (ReadOnlyMemory<byte>)GetValue(CodedContentProperty);
        set => SetValue(CodedContentProperty, value);
    }

    public static readonly DependencyProperty CodedContentProperty
        = DependencyProperty.Register(nameof(CodedContent),
                                      typeof(ReadOnlyMemory<byte>),
                                      typeof(HuffmanDecodeControl),
                                      new PropertyMetadata(default(ReadOnlyMemory<byte>)));

    /// <summary>
    /// Raised when a band is clicked, for a code table wanting to follow the symbol
    /// </summary>
    public event EventHandler<int>? SymbolInvoked;

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        e.Surface.Canvas.Clear(SKColors.Transparent);

        if (Steps is not { Count: > 0 } steps)
        {
            return;
        }

        ApplyTheme();

        _regions = _renderer.Draw(e.Surface.Canvas,
                                  steps,
                                  CodedContent,
                                  (float)WalkCanvas.ActualWidth,
                                  _scrollOffset);
    }

    private void ApplyTheme()
    {
        var isDark = ActualTheme == ElementTheme.Dark;

        _renderer.TextColour = isDark ? new SKColor(0xEC, 0xEB, 0xE6) : new SKColor(0x20, 0x20, 0x20);
        _renderer.MutedColour = isDark ? new SKColor(0x9A, 0x98, 0x92) : new SKColor(0x70, 0x70, 0x70);
        _renderer.UnusedColour = isDark ? new SKColor(0x5C, 0x5C, 0x58) : new SKColor(0xBB, 0xB9, 0xB2);
        _renderer.BoxColour = isDark ? new SKColor(0x24, 0x24, 0x22) : new SKColor(0xFA, 0xF9, 0xF6);
        _renderer.BorderColour = isDark ? new SKColor(0x44, 0x44, 0x41) : new SKColor(0xD0, 0xCE, 0xC6);
        _renderer.SelectionColour = isDark ? new SKColor(0xA5, 0xD6, 0xA7) : new SKColor(0x2E, 0x7D, 0x32);
        _renderer.LengthColour = isDark ? new SKColor(0x4A, 0x2A, 0x05) : new SKColor(0xFF, 0xE0, 0xB2);
        _renderer.WordBoxColour = isDark ? new SKColor(0x30, 0x30, 0x2E) : new SKColor(0xF0, 0xEF, 0xEA);

        _renderer.BandColours = isDark
            ? [new SKColor(0x24, 0x3C, 0x1C), new SKColor(0x32, 0x51, 0x28)]
            : [new SKColor(0xDC, 0xED, 0xC8), new SKColor(0xC5, 0xE1, 0xA5)];
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(WalkCanvas).Position;

        var region = _regions.FirstOrDefault(r => r.Bounds.Contains((float)point.X, (float)point.Y));

        if (region.Bounds.Height <= 0)
        {
            return;
        }

        // Picking the same code again clears it, there being nowhere else on the drawing to click to let go of it
        if (region.Index == SelectedStep)
        {
            SelectedStep = -1;

            SymbolInvoked?.Invoke(this, -1);

            return;
        }

        SelectedStep = region.Index;

        if (Steps is { } steps && region.Index < steps.Count)
        {
            SymbolInvoked?.Invoke(this, steps[region.Index].Symbol);
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(WalkCanvas).Properties.MouseWheelDelta;

        SetScroll(_scrollOffset - delta);

        e.Handled = true;
    }

    private void ScrollBar_OnScroll(object sender, ScrollEventArgs e) => SetScroll((float)e.NewValue);

    private void SetScroll(float offset)
    {
        _scrollOffset = Math.Clamp(offset, 0, (float)HorizontalScrollBar.Maximum);

        HorizontalScrollBar.Value = _scrollOffset;

        WalkCanvas.Invalidate();
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateScrollBar();

    /// <summary>
    /// The walk runs on rather than wrapping, so what it can outrun is the width
    /// </summary>
    private void UpdateScrollBar()
    {
        var width = (float)WalkCanvas.ActualWidth;

        var content = Steps is { Count: > 0 } steps ? _renderer.GetWidth(steps) : 0;

        HorizontalScrollBar.Maximum = Math.Max(0, content - width);
        HorizontalScrollBar.ViewportSize = width;
        HorizontalScrollBar.SmallChange = 24;
        HorizontalScrollBar.LargeChange = width;

        HorizontalScrollBar.Visibility = HorizontalScrollBar.Maximum > 0 ? Visibility.Visible : Visibility.Collapsed;

        SetScroll(_scrollOffset);
    }
    /// <summary>
    /// Releases the renderer, whose paints and fonts are native Skia handles the collector will not reclaim
    /// </summary>
    public void Dispose()
    {
        WalkCanvas.PaintSurface -= OnPaintSurface;
        WalkCanvas.PointerPressed -= OnPointerPressed;
        WalkCanvas.PointerWheelChanged -= OnPointerWheelChanged;

        _renderer.Dispose();
    }
}
