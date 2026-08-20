using System;
using System.Linq;
using InternalsViewer.UI.App.Models.Columnstore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// The tree a page's Huffman codes describe, scrolled a symbol at a time
/// </summary>
public sealed partial class HuffmanTreeControl : IDisposable
{
    private readonly HuffmanTreeRenderer _renderer = new();

    private float _scrollOffset;

    public HuffmanTreeControl()
    {
        InitializeComponent();

        TreeCanvas.PaintSurface += OnPaintSurface;
        TreeCanvas.PointerPressed += OnPointerPressed;
        TreeCanvas.PointerWheelChanged += OnPointerWheelChanged;

        ActualThemeChanged += (_, _) => TreeCanvas.Invalidate();
    }

    public HuffmanTreeNode? Tree
    {
        get => (HuffmanTreeNode?)GetValue(TreeProperty);
        set => SetValue(TreeProperty, value);
    }

    public static readonly DependencyProperty TreeProperty
        = DependencyProperty.Register(nameof(Tree),
                                      typeof(HuffmanTreeNode),
                                      typeof(HuffmanTreeControl),
                                      new PropertyMetadata(null, OnTreeChanged));

    private static void OnTreeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HuffmanTreeControl)d;

        control._scrollOffset = 0;
        control._renderer.SelectedSymbol = -1;

        control.EmptyText.Visibility = e.NewValue is null ? Visibility.Visible : Visibility.Collapsed;

        control.UpdateScrollBar();
        control.TreeCanvas.Invalidate();
    }

    /// <summary>
    /// Symbol picked out in the drawing, which the code table sets as its selection moves
    /// </summary>
    public int SelectedSymbol
    {
        get => (int)GetValue(SelectedSymbolProperty);
        set => SetValue(SelectedSymbolProperty, value);
    }

    public static readonly DependencyProperty SelectedSymbolProperty
        = DependencyProperty.Register(nameof(SelectedSymbol),
                                      typeof(int),
                                      typeof(HuffmanTreeControl),
                                      new PropertyMetadata(-1, OnSelectedSymbolChanged));

    private static void OnSelectedSymbolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HuffmanTreeControl)d;

        control._renderer.SelectedSymbol = (int)e.NewValue;

        control.TreeCanvas.Invalidate();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        e.Surface.Canvas.Clear(SKColors.Transparent);

        if (Tree is not { } tree)
        {
            return;
        }

        ApplyTheme();

        _renderer.Draw(e.Surface.Canvas, tree, _scrollOffset, (float)TreeCanvas.ActualHeight);
    }

    private void ApplyTheme()
    {
        var isDark = ActualTheme == ElementTheme.Dark;

        _renderer.TextColour = isDark ? new SKColor(0xEC, 0xEB, 0xE6) : new SKColor(0x20, 0x20, 0x20);
        _renderer.MutedColour = isDark ? new SKColor(0x9A, 0x98, 0x92) : new SKColor(0x70, 0x70, 0x70);
        _renderer.BranchColour = isDark ? new SKColor(0x5C, 0x5C, 0x58) : new SKColor(0xB0, 0xAE, 0xA6);
        _renderer.LeafColour = isDark ? new SKColor(0x85, 0xB7, 0xEB) : new SKColor(0x18, 0x5F, 0xA5);
        _renderer.SelectionColour = isDark ? new SKColor(0xF5, 0xA6, 0x23) : new SKColor(0xC2, 0x6A, 0x00);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Tree is not { } tree)
        {
            return;
        }

        var point = e.GetCurrentPoint(TreeCanvas).Position;

        var region = HuffmanTreeRenderer.GetLeafRegions(tree, _scrollOffset, (float)TreeCanvas.ActualWidth)
                              .FirstOrDefault(r => r.Bounds.Contains((float)point.X, (float)point.Y));

        if (region.Bounds.Height > 0)
        {
            SelectedSymbol = region.Symbol;
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(TreeCanvas).Properties.MouseWheelDelta;

        SetScroll(_scrollOffset - (delta / 120f * HuffmanTreeRenderer.RowHeight * 3));

        e.Handled = true;
    }

    private void ScrollBar_OnScroll(object sender, ScrollEventArgs e) => SetScroll((float)e.NewValue);

    private void SetScroll(float offset)
    {
        _scrollOffset = Math.Clamp(offset, 0, (float)VerticalScrollBar.Maximum);

        VerticalScrollBar.Value = _scrollOffset;

        TreeCanvas.Invalidate();
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateScrollBar();

    private void UpdateScrollBar()
    {
        var height = (float)TreeCanvas.ActualHeight;

        var content = Tree is { } tree ? HuffmanTreeRenderer.GetHeight(tree) : 0;

        VerticalScrollBar.Maximum = Math.Max(0, content - height);
        VerticalScrollBar.ViewportSize = height;
        VerticalScrollBar.SmallChange = HuffmanTreeRenderer.RowHeight;
        VerticalScrollBar.LargeChange = height;

        SetScroll(_scrollOffset);
    }
    /// <summary>
    /// Releases the renderer, whose paints and fonts are native Skia handles the collector will not reclaim
    /// </summary>
    public void Dispose() => _renderer.Dispose();
}
