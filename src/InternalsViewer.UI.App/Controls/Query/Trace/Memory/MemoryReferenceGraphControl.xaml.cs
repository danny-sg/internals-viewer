using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Memory;

public sealed partial class MemoryReferenceGraphControl : UserControl
{
    private const double ArrowLength = 6;

    private const double ArrowHalfWidth = 3;

    private const double EdgeLabelGap = 4;

    private const double EdgeLabelRise = 15;

    private const double EdgeLabelDrop = 1;

    private const double DashLength = 4;

    private const double DashGap = 3;

    private const double WheelZoomStep = 1.1;

    private const double WheelNotch = 120;

    private const float DetailZoom = 0.8f;

    private const double BandHeight = 4;

    private const double LifetimeHeight = 1;

    private const string RightGlyph = "\uE76C";

    private const string LeftGlyph = "\uE76B";

    private const string TruncatedPrefix = "Graph Limited to ";

    private const string TruncatedSuffix = " Objects";

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

        GraphScroller.AddHandler(PointerPressedEvent, new PointerEventHandler(OnScrollerPointerPressed), true);

        Loaded += OnLoaded;

        var menu = new MenuFlyout();

        menu.Items.Add(MenuItems.Create("Zoom to 100%", () => ZoomAt(GetViewportCentre(), 1)));

        menu.Items.Add(MenuItems.Create("Zoom to Fit", ZoomToFit));

        GraphScroller.ContextFlyout = menu;
    }

    private List<(MemoryReferenceGraphNode Node, PropertyChangedEventHandler Handler)> Subscriptions { get; } = [];

    private Point? DragOrigin { get; set; }

    private Point DragOffset { get; set; }

    private List<UIElement> ZoomDetails { get; } = [];

    private List<(Rectangle Bar, double Width)> ProgressBars { get; } = [];

    private bool IsDetailVisible { get; set; } = true;

    private Dictionary<Color, SolidColorBrush> MarkBrushes { get; } = [];

    private double Scale { get; set; } = 1;

    private void Unsubscribe()
    {
        foreach (var (node, handler) in Subscriptions)
        {
            node.PropertyChanged -= handler;
        }

        Subscriptions.Clear();
    }

    private void Rebuild()
    {
        Scale = XamlRoot?.RasterizationScale ?? 1;

        Unsubscribe();

        GraphCanvas.Children.Clear();

        ZoomDetails.Clear();

        ProgressBars.Clear();

        if (Graph is not { } graph)
        {
            GraphCanvas.Width = 0;

            GraphCanvas.Height = 0;

            TruncatedText.Visibility = Visibility.Collapsed;

            return;
        }

        GraphCanvas.Width = graph.Width;

        GraphCanvas.Height = graph.Height;

        foreach (var edge in graph.Edges)
        {
            AddEdge(edge);
        }

        foreach (var node in graph.Nodes)
        {
            AddNode(node, graph.NodeWidth, graph.NodeHeight);
        }

        TruncatedText.Text = $"{TruncatedPrefix}{graph.Nodes.Count}{TruncatedSuffix}";

        TruncatedText.Visibility = graph.IsTruncated ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddEdge(MemoryReferenceGraphEdge edge)
    {
        var points = new PointCollection();

        foreach (var point in edge.Points)
        {
            points.Add(Snap(point));
        }

        var thickness = GetLineThickness();

        var line = new Polyline
        {
            Points = points,
            Style = GetStyle(edge.IsHot ? "HotEdgeStyle" : "EdgeStyle"),
            StrokeThickness = thickness
        };

        SetExtent(line, points);

        if (edge.IsCrossLink)
        {
            line.StrokeDashArray = new DoubleCollection { DashLength / thickness, DashGap / thickness };
        }

        GraphCanvas.Children.Add(line);

        var end = Snap(edge.Points[^1]);

        var direction = end.X >= edge.Points[^2].X ? 1 : -1;

        var arrow = new Polygon
        {
            Points = new PointCollection
            {
                new Point(end.X - direction * ArrowLength, end.Y - ArrowHalfWidth),
                end,
                new Point(end.X - direction * ArrowLength, end.Y + ArrowHalfWidth)
            },
            Style = GetStyle(edge.IsHot ? "HotArrowStyle" : "ArrowStyle")
        };

        SetExtent(arrow, arrow.Points);

        GraphCanvas.Children.Add(arrow);

        var left = edge.LabelAt.X + EdgeLabelGap;

        AddEdgeLabel(edge, edge.Label, left, edge.LabelAt.Y + (edge.IsLabelBelow ? EdgeLabelDrop : -EdgeLabelRise));

        if (edge.Changes.Length > 0)
        {
            AddEdgeLabel(edge, edge.Changes, left, edge.LabelAt.Y + EdgeLabelDrop);
        }
    }

    private static void SetExtent(Shape shape, PointCollection points)
    {
        shape.Width = points.Max(p => p.X) + ArrowLength;

        shape.Height = points.Max(p => p.Y) + ArrowLength;
    }

    private double GetLineThickness() => Math.Max(1, Math.Round(Scale)) / Scale;

    private Point Snap(Point point) => new(Snap(point.X), Snap(point.Y));

    private double Snap(double value)
    {
        var centre = Math.Max(1, Math.Round(Scale)) % 2 == 1 ? 0.5 : 0;

        return (Math.Floor(value * Scale) + centre) / Scale;
    }

    private void AddEdgeLabel(MemoryReferenceGraphEdge edge, string text, double left, double top)
    {
        var label = new TextBlock
        {
            Text = text,
            Style = GetStyle(edge.IsHot ? "HotEdgeLabelStyle" : "EdgeLabelStyle"),
            Visibility = IsDetailVisible ? Visibility.Visible : Visibility.Collapsed
        };

        ToolTipService.SetToolTip(label, edge.Detail);

        Canvas.SetLeft(label, left);

        Canvas.SetTop(label, top);

        GraphCanvas.Children.Add(label);

        ZoomDetails.Add(label);
    }

    private void AddNode(MemoryReferenceGraphNode node, double width, double height)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var label = new TextBlock { Style = GetStyle("NodeLabelStyle") };

        HyperlinkButton? link = null;

        if (node.Open is { } open)
        {
            link = new HyperlinkButton { Content = label, Style = (Style)Application.Current.Resources["DetailLinkStyle"] };

            link.Click += (_, _) => open();

            text.Children.Add(link);
        }
        else
        {
            text.Children.Add(label);
        }

        var detail = new TextBlock { Style = GetStyle("NodeDetailStyle") };

        text.Children.Add(detail);

        Update();

        PropertyChangedEventHandler handler = (_, _) => Update();

        node.PropertyChanged += handler;

        Subscriptions.Add((node, handler));

        var content = new Grid { Style = GetStyle("NodeContentStyle") };

        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(text, 1);

        content.Children.Add(text);

        if (node.ToggleReferrers is { } toggleReferrers)
        {
            content.Children.Add(CreateToggle(node.IsReferrersExpanded ? RightGlyph : LeftGlyph,
                                              node.IsReferrersExpanded ? "Hide Referencing Objects" : "Show Referencing Objects",
                                              toggleReferrers,
                                              0));
        }

        if (node.Toggle is { } toggle)
        {
            content.Children.Add(CreateToggle(node.IsExpanded ? LeftGlyph : RightGlyph,
                                              node.IsExpanded ? "Collapse" : "Expand",
                                              toggle,
                                              2));
        }

        var border = new Border
        {
            Width = width,
            Height = height,
            Style = GetStyle(node.IsRoot ? "RootNodeStyle" : node.IsData ? "DataNodeStyle" : "NodeStyle")
        };

        var layout = new Grid();

        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        layout.Children.Add(CreateBand(node, width - border.BorderThickness.Left - border.BorderThickness.Right));

        Grid.SetRow(content, 1);

        layout.Children.Add(content);

        border.Child = layout;

        Canvas.SetLeft(border, node.X);

        Canvas.SetTop(border, node.Y);

        GraphCanvas.Children.Add(border);

        void Update()
        {
            label.Text = node.Label;

            detail.Text = node.Detail;

            if (link is not null)
            {
                link.IsEnabled = node.CanOpen;
            }

            string[] tip = [node.Label, node.Label == node.Types ? string.Empty : node.Types, node.Detail, node.DataChanges];

            ToolTipService.SetToolTip(text, string.Join(Environment.NewLine, tip.Where(t => t.Length > 0)));
        }
    }

    private Button CreateToggle(string glyph, string tip, Action toggle, int column)
    {
        var button = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 10 }, Style = GetStyle("ToggleStyle") };

        ToolTipService.SetToolTip(button, tip);

        button.Click += (_, _) => toggle();

        Grid.SetColumn(button, column);

        return button;
    }

    private Canvas CreateBand(MemoryReferenceGraphNode node, double width)
    {
        var band = new Canvas
        {
            Width = width,
            Height = BandHeight + LifetimeHeight,
            IsHitTestVisible = false,
            Visibility = IsDetailVisible ? Visibility.Visible : Visibility.Collapsed
        };

        var track = new Rectangle { Height = LifetimeHeight, Width = width, Style = GetStyle("ProgressStyle") };

        Canvas.SetTop(track, BandHeight);

        band.Children.Add(track);

        foreach (var (start, end) in node.ActiveSpans)
        {
            var span = new Rectangle
            {
                Height = LifetimeHeight,
                Width = Math.Max(1, (end - start) * width),
                Style = GetStyle("ActiveStyle")
            };

            Canvas.SetLeft(span, start * width);

            Canvas.SetTop(span, BandHeight);

            band.Children.Add(span);
        }

        var bar = new Rectangle { Height = BandHeight, Width = Progress * width, Style = GetStyle("ProgressStyle") };

        band.Children.Add(bar);

        ProgressBars.Add((bar, width));

        var columns = Math.Max(1, (int)width);

        var colours = new Color?[columns];

        foreach (var mark in node.Marks)
        {
            colours[Math.Clamp((int)(mark.At * columns), 0, columns - 1)] = mark.Colour;
        }

        var marks = new Dictionary<Color, GeometryCollection>();

        for (var column = 0; column < columns; column++)
        {
            if (colours[column] is not { } colour)
            {
                continue;
            }

            var start = column;

            while (column + 1 < columns && colours[column + 1] == colour)
            {
                column++;
            }

            if (!marks.TryGetValue(colour, out var geometry))
            {
                geometry = new GeometryCollection();

                marks[colour] = geometry;
            }

            geometry.Add(new RectangleGeometry { Rect = new Rect(start, 0, column - start + 1, BandHeight) });
        }

        foreach (var (colour, geometry) in marks)
        {
            band.Children.Add(new Path { Data = new GeometryGroup { Children = geometry }, Fill = GetBrush(colour) });
        }

        ZoomDetails.Add(band);

        return band;
    }

    private SolidColorBrush GetBrush(Color colour)
    {
        if (!MarkBrushes.TryGetValue(colour, out var brush))
        {
            brush = new SolidColorBrush(colour);

            MarkBrushes[colour] = brush;
        }

        return brush;
    }

    private bool IsBackground(object source)
    {
        for (var element = source as DependencyObject; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ScrollBar || (element is Border && VisualTreeHelper.GetParent(element) == GraphCanvas))
            {
                return false;
            }

            if (element == GraphScroller)
            {
                return true;
            }
        }

        return false;
    }

    private void OnScrollerPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(GraphScroller);

        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch
            || !point.Properties.IsLeftButtonPressed
            || !IsBackground(e.OriginalSource))
        {
            return;
        }

        DragOrigin = point.Position;

        DragOffset = new Point(GraphScroller.HorizontalOffset, GraphScroller.VerticalOffset);

        GraphScroller.CapturePointer(e.Pointer);

        e.Handled = true;
    }

    private void OnScrollerPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (DragOrigin is not { } origin)
        {
            return;
        }

        var position = e.GetCurrentPoint(GraphScroller).Position;

        GraphScroller.ChangeView(DragOffset.X - (position.X - origin.X), DragOffset.Y - (position.Y - origin.Y), null, true);

        e.Handled = true;
    }

    private void OnScrollerPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (DragOrigin is null)
        {
            return;
        }

        DragOrigin = null;

        GraphScroller.ReleasePointerCapture(e.Pointer);

        e.Handled = true;
    }

    private void OnScrollerPointerCaptureLost(object sender, PointerRoutedEventArgs e) => DragOrigin = null;

    private void OnCanvasPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(GraphScroller);

        if (point.Properties.IsHorizontalMouseWheel || (e.KeyModifiers & VirtualKeyModifiers.Control) == 0)
        {
            return;
        }

        ZoomAt(point.Position, GraphScroller.ZoomFactor * Math.Pow(WheelZoomStep, point.Properties.MouseWheelDelta / WheelNotch));

        e.Handled = true;
    }

    private void ZoomAt(Point position, double zoom)
    {
        var target = Math.Clamp(zoom, GraphScroller.MinZoomFactor, GraphScroller.MaxZoomFactor);

        var scale = target / GraphScroller.ZoomFactor;

        GraphScroller.ChangeView((GraphScroller.HorizontalOffset + position.X) * scale - position.X,
                                 (GraphScroller.VerticalOffset + position.Y) * scale - position.Y,
                                 (float)target,
                                 true);
    }

    private void ZoomToFit()
    {
        if (Graph is not { Width: > 0, Height: > 0 } graph)
        {
            return;
        }

        var zoom = Math.Min(GraphScroller.ViewportWidth / graph.Width, GraphScroller.ViewportHeight / graph.Height);

        GraphScroller.ChangeView(0, 0, (float)Math.Clamp(zoom, GraphScroller.MinZoomFactor, GraphScroller.MaxZoomFactor), true);
    }

    private Point GetViewportCentre() => new(GraphScroller.ViewportWidth / 2, GraphScroller.ViewportHeight / 2);

    private void OnScrollerSizeChanged(object sender, SizeChangedEventArgs e) => FillViewport();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Graph is not null && XamlRoot?.RasterizationScale is { } scale && scale != Scale)
        {
            Rebuild();
        }
    }

    private void OnScrollerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        FillViewport();

        var isVisible = GraphScroller.ZoomFactor >= DetailZoom;

        if (isVisible == IsDetailVisible)
        {
            return;
        }

        IsDetailVisible = isVisible;

        foreach (var detail in ZoomDetails)
        {
            detail.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void FillViewport()
    {
        var zoom = GraphScroller.ZoomFactor;

        GraphCanvas.MinWidth = Math.Floor(GraphScroller.ViewportWidth / zoom);

        GraphCanvas.MinHeight = Math.Floor(GraphScroller.ViewportHeight / zoom);
    }

    private Style GetStyle(string key) => (Style)Resources[key];

    private void UpdateProgress()
    {
        foreach (var (bar, width) in ProgressBars)
        {
            bar.Width = Progress * width;
        }
    }

    private static void OnGraphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MemoryReferenceGraphControl)d).Rebuild();

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MemoryReferenceGraphControl)d).UpdateProgress();
}
