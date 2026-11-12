using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Lineage;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Memory;

public sealed partial class LineageGraphControl : UserControl
{
    private const double CellPadding = 8;

    private const double HeaderPadding = 4;

    private const double TextSize = 12;

    private const double SubtitleSize = 11;

    private const double LabelSize = 11;

    private const double LinkStub = 14;

    private const double BackGap = 10;

    private const double DashLength = 4;

    private const double DashGap = 3;

    private const string BoxBorderKey = "CardStrokeColorDefaultBrush";

    private const string BoxBackgroundKey = "SolidBackgroundFillColorBaseBrush";

    private const string HeaderBackgroundKey = "SolidBackgroundFillColorSecondaryBrush";

    private const string SubtitleKey = "TextFillColorSecondaryBrush";

    private const string LabelKey = "TextFillColorTertiaryBrush";

    private const string WarningKey = "SystemFillColorCriticalBrush";

    private const string LinkKey = "ControlStrongStrokeColorDefaultBrush";

    private const string MonospaceKey = "MonospaceFontFamily";

    private const string TitleStyleKey = "DetailLinkStyle";

    private const string PointerStyleKey = "ResultPointerStyle";

    public static readonly DependencyProperty GraphProperty =
        DependencyProperty.Register(nameof(Graph),
                                    typeof(LineageGraph),
                                    typeof(LineageGraphControl),
                                    new PropertyMetadata(null, OnGraphChanged));

    public LineageGraph? Graph
    {
        get => (LineageGraph?)GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    public LineageGraphControl()
    {
        InitializeComponent();
    }

    private List<(LineageRow Row, TextBlock? Pointer, TextBlock Value)> Cells { get; } = [];

    private static Point Snap(double x, double y) => new(Math.Floor(x) + 0.5, Math.Floor(y) + 0.5);

    private static double GetValueLine(int index)
        => LineageGraph.HeaderHeight + (index + 1) * LineageGraph.RowHeight - LineageGraph.LineHeight / 2;

    private void Build()
    {
        Surface.Children.Clear();

        Cells.Clear();

        WarningBar.IsOpen = Graph?.Warnings.Count > 0;

        WarningBar.Message = string.Join(Environment.NewLine, Graph?.Warnings ?? []);

        if (Graph is not { } graph)
        {
            Surface.Width = 0;

            Surface.Height = 0;

            return;
        }

        Surface.Width = graph.Width;

        Surface.Height = graph.Height;

        foreach (var box in graph.Boxes)
        {
            for (var index = 0; index < box.Rows.Count; index++)
            {
                if (box.Rows[index].Target is { } target)
                {
                    Surface.Children.Add(CreateLink(graph, box, index, target));
                }
            }
        }

        foreach (var box in graph.Boxes)
        {
            Surface.Children.Add(CreateBox(graph, box));
        }

        UpdateCells();
    }

    private Polyline CreateLink(LineageGraph graph, LineageBox box, int index, LineageBox target)
    {
        var startX = box.X + LineageGraph.BoxWidth;

        var startY = box.Y + GetValueLine(index);

        var isForward = target.X > box.X;

        var points = new PointCollection { Snap(startX, startY) };

        if (isForward)
        {
            var middle = startX + LineageGraph.ColumnGap / 2;

            var targetRow = target.Rows.FindIndex(r => r.Offset == box.Rows[index].TargetOffset);

            var endY = box.Rows[index].TargetOffset >= 0 && targetRow >= 0
                ? target.Y + GetValueLine(targetRow)
                : target.Y + LineageGraph.HeaderHeight / 2;

            points.Add(Snap(middle, startY));

            points.Add(Snap(middle, endY));

            points.Add(Snap(target.X, endY));
        }
        else
        {
            var right = startX + LinkStub;

            var top = target.Y - BackGap;

            var centre = target.X + LineageGraph.BoxWidth / 2;

            points.Add(Snap(right, startY));

            points.Add(Snap(right, top));

            points.Add(Snap(centre, top));

            points.Add(Snap(centre, target.Y));
        }

        var link = new Polyline
        {
            Points = points,
            Stroke = AppResources.Get<Brush>(LinkKey),
            StrokeThickness = 1,
            Width = graph.Width,
            Height = graph.Height
        };

        if (!isForward || box.Rows[index].IsIndex)
        {
            link.StrokeDashArray = new DoubleCollection { DashLength, DashGap };
        }

        return link;
    }

    private Border CreateBox(LineageGraph graph, LineageBox box)
    {
        var title = new HyperlinkButton
        {
            Style = AppResources.Get<Style>(TitleStyleKey),
            Content = new TextBlock
            {
                Text = box.Title,
                FontSize = TextSize,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };

        title.Click += (_, _) => graph.Open(box.Address);

        var subtitle = new TextBlock
        {
            FontSize = SubtitleSize,
            Foreground = AppResources.Get<Brush>(SubtitleKey),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        HexText.Apply(subtitle, box.Subtitle);

        var panel = new StackPanel
        {
            Children =
            {
                new StackPanel
                {
                    Height = LineageGraph.HeaderHeight,
                    Padding = new Thickness(CellPadding, HeaderPadding, CellPadding, HeaderPadding),
                    Background = AppResources.Get<Brush>(HeaderBackgroundKey),
                    Children = { title, subtitle }
                }
            }
        };

        foreach (var row in box.Rows)
        {
            panel.Children.Add(CreateRow(graph, row));
        }

        var border = new Border
        {
            Width = LineageGraph.BoxWidth,
            BorderThickness = new Thickness(1),
            BorderBrush = AppResources.Get<Brush>(BoxBorderKey),
            Background = AppResources.Get<Brush>(BoxBackgroundKey),
            Child = panel
        };

        Canvas.SetLeft(border, box.X);

        Canvas.SetTop(border, box.Y);

        return border;
    }

    private StackPanel CreateRow(LineageGraph graph, LineageRow row)
    {
        var label = new TextBlock
        {
            Text = row.Name,
            Height = LineageGraph.LineHeight,
            FontSize = LabelSize,
            Foreground = AppResources.Get<Brush>(LabelKey),
            Padding = new Thickness(CellPadding, 0, CellPadding, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        if (row.UnexpectedWriter is { } writer)
        {
            label.Foreground = AppResources.Get<Brush>(WarningKey);

            ToolTipService.SetToolTip(label, $"Written By {writer}");
        }

        var grid = new Grid
        {
            Height = LineageGraph.LineHeight,
            Padding = new Thickness(CellPadding, 0, CellPadding, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            }
        };

        TextBlock? pointer = null;

        if (row.HasPointer)
        {
            pointer = new TextBlock { FontSize = TextSize, FontFamily = AppResources.Get<FontFamily>(MonospaceKey) };

            var link = new HyperlinkButton
            {
                Style = AppResources.Get<Style>(PointerStyleKey),
                Padding = new Thickness(0, 0, CellPadding, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Content = pointer
            };

            link.Click += (_, _) => graph.Open(row.Pointer);

            grid.Children.Add(link);
        }

        var value = new TextBlock
        {
            FontSize = TextSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        Grid.SetColumn(value, 1);

        grid.Children.Add(value);

        Cells.Add((row, pointer, value));

        return new StackPanel { Children = { label, grid } };
    }

    private void UpdateCells()
    {
        foreach (var (row, pointer, value) in Cells)
        {
            if (pointer is not null)
            {
                pointer.Text = row.PointerText;
            }

            value.Text = row.Value;

            ToolTipService.SetToolTip(value, row.Value.Length > 0 ? row.Value : null);
        }
    }

    private void OnStructureChanged(object? sender, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            Build();
        }
        else
        {
            DispatcherQueue.TryEnqueue(Build);
        }
    }

    private void OnValuesChanged(object? sender, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            UpdateCells();
        }
        else
        {
            DispatcherQueue.TryEnqueue(UpdateCells);
        }
    }

    private static void OnGraphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LineageGraphControl)d;

        if (e.OldValue is LineageGraph old)
        {
            old.Changed -= control.OnValuesChanged;

            old.Rebuilt -= control.OnStructureChanged;
        }

        if (e.NewValue is LineageGraph graph)
        {
            graph.Changed += control.OnValuesChanged;

            graph.Rebuilt += control.OnStructureChanged;
        }

        control.Build();
    }
}
