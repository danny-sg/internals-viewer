using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.UI.App.Controls.Query.Trace.Memory.Enums;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Memory;

public sealed partial class MemoryReferenceGraphControl : UserControl, IDisposable
{
    private const double DragThreshold = 3;

    private const double ToolTipGap = 14;

    private const double ToolTipPadding = 12;

    private const float SelectionMinimum = 8;

    public static readonly DependencyProperty GraphProperty =
        DependencyProperty.Register(nameof(Graph),
                                    typeof(MemoryReferenceGraph),
                                    typeof(MemoryReferenceGraphControl),
                                    new PropertyMetadata(null, OnGraphChanged));

    public MemoryReferenceGraph? Graph
    {
        get => (MemoryReferenceGraph?)GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress),
                                    typeof(double),
                                    typeof(MemoryReferenceGraphControl),
                                    new PropertyMetadata(0d, OnProgressChanged));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public MemoryReferenceGraphControl()
    {
        InitializeComponent();

        GraphCanvas.PaintSurface += OnPaintSurface;
        GraphCanvas.PointerPressed += OnPointerPressed;
        GraphCanvas.PointerMoved += OnPointerMoved;
        GraphCanvas.PointerReleased += OnPointerReleased;
        GraphCanvas.PointerCaptureLost += OnPointerCaptureLost;
        GraphCanvas.PointerExited += OnPointerExited;
        GraphCanvas.PointerWheelChanged += OnPointerWheelChanged;
        GraphCanvas.RightTapped += OnRightTapped;
        GraphCanvas.SizeChanged += OnCanvasSizeChanged;

        ActualThemeChanged += OnActualThemeChanged;

        Loaded += OnLoaded;
    }

    private MemoryReferenceGraphPaints Paints { get; } = new();

    private CanvasViewport Viewport { get; } = new();

    private bool IsThemeDirty { get; set; } = true;

    private bool IsPressed { get; set; }

    private bool IsPanning { get; set; }

    private bool IsSelecting { get; set; }

    private bool IsShiftPressed { get; set; }

    private Point PressPoint { get; set; }

    private Point DragPoint { get; set; }

    private SKPoint PressOffset { get; set; }

    private GraphHit PressHit { get; set; }

    private GraphHit HoverHit { get; set; }

    private MemoryReferenceGraphNode? HoveredNode { get; set; }

    private MemoryReferenceGraphNode? SelectedNode { get; set; }

    private List<INotifyPropertyChanged> Subscribed { get; } = [];

    private InputSystemCursorShape CursorShape { get; set; } = InputSystemCursorShape.Arrow;

    public void ShowNode(MemoryReferenceGraphNode node)
    {
        if (Graph is not { } graph)
        {
            return;
        }

        SelectedNode = node;

        if (Viewport.Zoom < DetailZoom)
        {
            Viewport.ZoomTo(1, 0, 0);
        }

        Viewport.CentreOn(new SKPoint((float)(node.X + graph.NodeWidth / 2), (float)(node.Y + graph.NodeHeight / 2)));

        SyncScrollBars();

        GraphCanvas.Invalidate();
    }

    public void Dispose()
    {
        GraphCanvas.PaintSurface -= OnPaintSurface;
        GraphCanvas.PointerPressed -= OnPointerPressed;
        GraphCanvas.PointerMoved -= OnPointerMoved;
        GraphCanvas.PointerReleased -= OnPointerReleased;
        GraphCanvas.PointerCaptureLost -= OnPointerCaptureLost;
        GraphCanvas.PointerExited -= OnPointerExited;
        GraphCanvas.PointerWheelChanged -= OnPointerWheelChanged;
        GraphCanvas.RightTapped -= OnRightTapped;
        GraphCanvas.SizeChanged -= OnCanvasSizeChanged;

        ActualThemeChanged -= OnActualThemeChanged;

        Loaded -= OnLoaded;

        Unsubscribe();

        PathBuilder.Dispose();

        Paints.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        IsThemeDirty = true;

        UpdateExtent();

        GraphCanvas.Invalidate();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        IsThemeDirty = true;

        GraphCanvas.Invalidate();
    }

    private void ApplyTheme()
    {
        Paints.Line = GetColour(LineProbe, Paints.Line);
        Paints.CrossLine = GetColour(CrossLineProbe, Paints.CrossLine);
        Paints.HoverLine = GetColour(HoverLineProbe, Paints.HoverLine);
        Paints.HotLine = GetColour(HotLineProbe, Paints.HotLine);
        Paints.Progress = GetColour(ProgressProbe, Paints.Progress);
        Paints.Active = GetColour(ActiveProbe, Paints.Active);
        Paints.Node = GetColour(NodeProbe, Paints.Node);
        Paints.DataNode = GetColour(DataNodeProbe, Paints.DataNode);
        Paints.Accent = GetColour(AccentProbe, Paints.Accent);
        Paints.Text = GetColour(TextProbe, Paints.Text);
        Paints.SecondaryText = GetColour(SecondaryTextProbe, Paints.SecondaryText);
        Paints.DisabledText = GetColour(DisabledTextProbe, Paints.DisabledText);
        Paints.Link = GetColour(LinkProbe, Paints.Link);
        Paints.HoverFill = GetColour(HoverProbe, Paints.HoverFill);
    }

    private static SKColor GetColour(Border probe, SKColor fallback)
        => probe.Background is SolidColorBrush brush
            ? new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A)
            : fallback;

    private void SetGraph(MemoryReferenceGraph? graph)
    {
        Unsubscribe();

        SetHover(default);

        HideToolTip();

        SelectedNode = null;

        BuildScene(graph);

        if (graph is not null)
        {
            foreach (var item in graph.Nodes.Concat<INotifyPropertyChanged>(graph.Edges))
            {
                item.PropertyChanged += OnItemChanged;

                Subscribed.Add(item);
            }
        }

        UpdateExtent();

        GraphCanvas.Invalidate();
    }

    private void Unsubscribe()
    {
        foreach (var item in Subscribed)
        {
            item.PropertyChanged -= OnItemChanged;
        }

        Subscribed.Clear();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => GraphCanvas.Invalidate();

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(GraphCanvas);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        IsPressed = true;

        IsPanning = false;

        IsSelecting = false;

        IsShiftPressed = (e.KeyModifiers & VirtualKeyModifiers.Shift) != 0;

        PressPoint = point.Position;

        DragPoint = point.Position;

        PressOffset = new SKPoint(Viewport.OffsetX, Viewport.OffsetY);

        PressHit = HitTest(point.Position);

        HideToolTip();

        GraphCanvas.CapturePointer(e.Pointer);

        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(GraphCanvas).Position;

        if (!IsPressed)
        {
            UpdateHover(position);

            return;
        }

        DragPoint = position;

        if (!IsPanning && !IsSelecting && Distance(PressPoint, position) > DragThreshold)
        {
            IsSelecting = IsShiftPressed;

            IsPanning = !IsShiftPressed;

            SetCursor(IsPanning ? InputSystemCursorShape.SizeAll : InputSystemCursorShape.Cross);
        }

        if (IsPanning && Viewport.SetOffset(PressOffset.X - (float)(position.X - PressPoint.X),
                                            PressOffset.Y - (float)(position.Y - PressPoint.Y)))
        {
            SyncScrollBars();

            GraphCanvas.Invalidate();
        }
        else if (IsSelecting)
        {
            GraphCanvas.Invalidate();
        }

        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!IsPressed)
        {
            return;
        }

        if (IsSelecting)
        {
            ZoomToSelection();
        }
        else if (!IsPanning)
        {
            Click(PressHit);
        }

        EndPointer();

        GraphCanvas.ReleasePointerCapture(e.Pointer);

        UpdateHover(e.GetCurrentPoint(GraphCanvas).Position);

        e.Handled = true;
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndPointer();

    private void EndPointer()
    {
        var isSelecting = IsSelecting;

        IsPressed = false;

        IsPanning = false;

        IsSelecting = false;

        SetCursor(InputSystemCursorShape.Arrow);

        if (isSelecting)
        {
            GraphCanvas.Invalidate();
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (IsPressed)
        {
            return;
        }

        SetHover(default);

        HideToolTip();

        SetCursor(InputSystemCursorShape.Arrow);
    }

    private void Click(GraphHit hit)
    {
        switch (hit.Part)
        {
            case MemoryReferenceGraphPart.Expand:
            {
                hit.Node?.Toggle?.Invoke();

                break;
            }
            case MemoryReferenceGraphPart.Referrers:
            {
                hit.Node?.ToggleReferrers?.Invoke();

                break;
            }
            case MemoryReferenceGraphPart.Name when hit.Node is { CanOpen: true } node:
            {
                node.Open?.Invoke();

                break;
            }
            case MemoryReferenceGraphPart.None:
            {
                SelectedNode = null;

                GraphCanvas.Invalidate();

                break;
            }
        }
    }

    private void UpdateHover(Point position)
    {
        var hit = HitTest(position);

        SetHover(hit);

        SetCursor(IsClickable(hit) ? InputSystemCursorShape.Hand : InputSystemCursorShape.Arrow);

        if (GetToolTipText(hit) is { Length: > 0 } text)
        {
            OpenToolTip(text, position);
        }
        else
        {
            HideToolTip();
        }
    }

    private void SetHover(GraphHit hit)
    {
        if (!ReferenceEquals(hit.Node, HoveredNode))
        {
            HoveredNode?.Hover?.Invoke(false);

            HoveredNode = hit.Node;

            HoveredNode?.Hover?.Invoke(true);
        }

        if (hit != HoverHit)
        {
            HoverHit = hit;

            GraphCanvas.Invalidate();
        }
    }

    private static bool IsClickable(GraphHit hit)
        => hit.Part is MemoryReferenceGraphPart.Expand or MemoryReferenceGraphPart.Referrers
           || (hit.Part == MemoryReferenceGraphPart.Name && hit.Node is { CanOpen: true });

    private string? GetToolTipText(GraphHit hit)
        => hit.Part switch
        {
            MemoryReferenceGraphPart.Expand => hit.Node is { IsExpanded: true } ? "Collapse" : "Expand",
            MemoryReferenceGraphPart.Referrers => hit.Node is { IsReferrersExpanded: true }
                ? "Hide Referencing Objects"
                : "Show Referencing Objects",
            MemoryReferenceGraphPart.Name or MemoryReferenceGraphPart.Node when hit.Node is { } node => GetNodeToolTip(node),
            MemoryReferenceGraphPart.Label or MemoryReferenceGraphPart.Edge when hit.Edge is { } edge => GetEdgeToolTip(edge),
            _ => null
        };

    private static string GetNodeToolTip(MemoryReferenceGraphNode node)
    {
        string[] lines = [node.Label, node.Label == node.Types ? string.Empty : node.Types, node.Detail, node.DataChanges];

        return string.Join(Environment.NewLine, lines.Where(l => l.Length > 0));
    }

    private static string GetEdgeToolTip(MemoryReferenceGraphEdge edge)
        => $"{edge.From.Label} → {edge.To.Label}{Environment.NewLine}{edge.Detail}";

    private void OpenToolTip(string text, Point position)
    {
        ToolTipText.Text = text;

        ToolTipPopup.IsOpen = true;

        ToolTipText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = ToolTipText.DesiredSize.Width + ToolTipPadding;

        var left = position.X + ToolTipGap + width > GraphCanvas.ActualWidth
            ? position.X - ToolTipGap - width
            : position.X + ToolTipGap;

        ToolTipPopup.HorizontalOffset = Math.Max(0, left);

        ToolTipPopup.VerticalOffset = position.Y + ToolTipGap;
    }

    private void HideToolTip() => ToolTipPopup.IsOpen = false;

    private void SetCursor(InputSystemCursorShape shape)
    {
        if (shape == CursorShape)
        {
            return;
        }

        CursorShape = shape;

        ProtectedCursor = InputSystemCursor.Create(shape);
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(GraphCanvas);

        var delta = point.Properties.MouseWheelDelta;

        HideToolTip();

        var changed = point.Properties.IsHorizontalMouseWheel
            ? Viewport.SetOffset(Viewport.OffsetX + delta, Viewport.OffsetY)
            : IsKeyDown(VirtualKey.Control)
                ? Viewport.ZoomAt(delta, point.Position.X, point.Position.Y, point.Timestamp)
                : IsKeyDown(VirtualKey.Shift)
                    ? Viewport.SetOffset(Viewport.OffsetX - delta, Viewport.OffsetY)
                    : Viewport.SetOffset(Viewport.OffsetX, Viewport.OffsetY - delta);

        if (changed)
        {
            SyncScrollBars();

            GraphCanvas.Invalidate();
        }

        e.Handled = true;
    }

    private static bool IsKeyDown(VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var point = e.GetPosition(GraphCanvas);

        var menu = new MenuFlyout();

        if (HitTest(point).Node is { } node)
        {
            menu.Items.Add(CreateMenuItem("Select", node.Select));

            menu.Items.Add(new MenuFlyoutSeparator());

            menu.Items.Add(CreateMenuItem("Expand All", node.ExpandAll));

            menu.Items.Add(CreateMenuItem("Collapse All", node.CollapseAll));

            menu.Items.Add(new MenuFlyoutSeparator());
        }
        else
        {
            menu.Items.Add(CreateMenuItem("Reset", Graph?.Reset));

            menu.Items.Add(new MenuFlyoutSeparator());
        }

        menu.Items.Add(MenuItems.Create("Zoom to 100%", () => ZoomTo(1)));

        menu.Items.Add(MenuItems.Create("Zoom to Fit", ZoomToFit));

        HideToolTip();

        menu.ShowAt(GraphCanvas, point);

        e.Handled = true;
    }

    private static MenuFlyoutItem CreateMenuItem(string text, Action? click)
    {
        var item = MenuItems.Create(text, () => click?.Invoke());

        item.IsEnabled = click is not null;

        return item;
    }

    private void ZoomTo(float zoom)
    {
        if (Viewport.ZoomTo(zoom, GraphCanvas.ActualWidth / 2, GraphCanvas.ActualHeight / 2))
        {
            SyncScrollBars();

            GraphCanvas.Invalidate();
        }
    }

    private void ZoomToFit()
    {
        if (Graph is not { } graph)
        {
            return;
        }

        Viewport.Fit(new SKRect(0, 0, (float)graph.Width, (float)graph.Height));

        SyncScrollBars();

        GraphCanvas.Invalidate();
    }

    private void ZoomToSelection()
    {
        var from = Viewport.ToContent(PressPoint.X, PressPoint.Y);

        var to = Viewport.ToContent(DragPoint.X, DragPoint.Y);

        var rect = new SKRect(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Max(from.X, to.X), Math.Max(from.Y, to.Y));

        if (rect.Width * Viewport.Zoom < SelectionMinimum || rect.Height * Viewport.Zoom < SelectionMinimum)
        {
            return;
        }

        Viewport.Fit(rect);

        SyncScrollBars();

        GraphCanvas.Invalidate();
    }

    private void OnScrollBarScroll(object sender, ScrollEventArgs e)
    {
        var horizontal = ReferenceEquals(sender, HorizontalScrollBar) ? (float)e.NewValue : Viewport.OffsetX;

        var vertical = ReferenceEquals(sender, VerticalScrollBar) ? (float)e.NewValue : Viewport.OffsetY;

        if (Viewport.SetOffset(horizontal, vertical))
        {
            SyncScrollBars();

            GraphCanvas.Invalidate();
        }
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateExtent();

        GraphCanvas.Invalidate();
    }

    private void UpdateExtent()
    {
        var graph = Graph;

        Viewport.SetExtent((float)(graph?.Width ?? 0),
                           (float)(graph?.Height ?? 0),
                           (float)GraphCanvas.ActualWidth,
                           (float)GraphCanvas.ActualHeight);

        SyncScrollBars();
    }

    private void SyncScrollBars()
    {
        VerticalScrollBar.Maximum = Viewport.MaximumOffsetY;
        VerticalScrollBar.ViewportSize = GraphCanvas.ActualHeight;
        VerticalScrollBar.Visibility = Viewport.MaximumOffsetY > 0 ? Visibility.Visible : Visibility.Collapsed;
        VerticalScrollBar.Value = Viewport.OffsetY;

        HorizontalScrollBar.Maximum = Viewport.MaximumOffsetX;
        HorizontalScrollBar.ViewportSize = GraphCanvas.ActualWidth;
        HorizontalScrollBar.Visibility = Viewport.MaximumOffsetX > 0 ? Visibility.Visible : Visibility.Collapsed;
        HorizontalScrollBar.Value = Viewport.OffsetX;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static void OnGraphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MemoryReferenceGraphControl)d).SetGraph(e.NewValue as MemoryReferenceGraph);

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MemoryReferenceGraphControl)d).GraphCanvas.Invalidate();

    private readonly record struct GraphHit(MemoryReferenceGraphNode? Node,
                                            MemoryReferenceGraphPart Part,
                                            MemoryReferenceGraphEdge? Edge);
}
