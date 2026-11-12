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

    private const double TextSize = 12;

    private const double TypeSize = 11;

    private const double ArrowLength = 7;

    private const double ArrowWidth = 4;

    private const string BoxBorderKey = "CardStrokeColorDefaultBrush";

    private const string BoxBackgroundKey = "SolidBackgroundFillColorBaseBrush";

    private const string HeaderBackgroundKey = "SolidBackgroundFillColorSecondaryBrush";

    private const string TypeKey = "TextFillColorSecondaryBrush";

    private const string LinkKey = "ControlStrongStrokeColorDefaultBrush";

    private const string LinkStyleKey = "DetailLinkStyle";

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

    private List<(LineageStage Stage, TextBlock Value)> Cells { get; } = [];

    private static Point Snap(double x, double y) => new(Math.Floor(x) + 0.5, Math.Floor(y) + 0.5);

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

        foreach (var stage in graph.Stages)
        {
            if (stage.Consumer is { } consumer)
            {
                AddArrow(graph, stage, consumer);
            }
        }

        foreach (var stage in graph.Stages)
        {
            Surface.Children.Add(CreateBox(graph, stage));
        }

        UpdateCells();
    }

    private void AddArrow(LineageGraph graph, LineageStage input, LineageStage consumer)
    {
        var brush = AppResources.Get<Brush>(LinkKey);

        var startX = input.X + LineageGraph.BoxWidth;

        var startY = input.Y + LineageGraph.BoxHeight / 2;

        var endX = consumer.X - ArrowLength;

        var endY = consumer.Y + LineageGraph.BoxHeight / 2;

        var middle = startX + (endX - startX) / 2;

        Surface.Children.Add(new Polyline
        {
            Points = { Snap(startX, startY), Snap(middle, startY), Snap(middle, endY), Snap(endX, endY) },
            Stroke = brush,
            StrokeThickness = 1,
            Width = graph.Width,
            Height = graph.Height
        });

        Surface.Children.Add(new Polygon
        {
            Points =
            {
                Snap(consumer.X, endY),
                Snap(endX, endY - ArrowWidth),
                Snap(endX, endY + ArrowWidth)
            },
            Fill = brush,
            Width = graph.Width,
            Height = graph.Height
        });
    }

    private Border CreateBox(LineageGraph graph, LineageStage stage)
    {
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(LineageGraph.OperatorHeight) },
                new RowDefinition { Height = new GridLength(LineageGraph.LineHeight) },
                new RowDefinition { Height = new GridLength(LineageGraph.LineHeight) }
            }
        };

        var iterator = stage.Stage.Iterator != 0 ? stage.Stage.Iterator : stage.Stage.PlanObject;

        var header = new Border
        {
            Padding = new Thickness(CellPadding, 0, CellPadding, 0),
            Background = AppResources.Get<Brush>(HeaderBackgroundKey),
            Child = CreateText(graph, stage.Operator, iterator, true)
        };

        grid.Children.Add(header);

        var type = CreateText(graph, stage.Type, stage.Stage.Address, false);

        Grid.SetRow(type, 1);

        grid.Children.Add(type);

        var value = new TextBlock
        {
            FontSize = TextSize,
            Padding = new Thickness(CellPadding, 0, CellPadding, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        Grid.SetRow(value, 2);

        grid.Children.Add(value);

        Cells.Add((stage, value));

        var border = new Border
        {
            Width = LineageGraph.BoxWidth,
            Height = LineageGraph.BoxHeight,
            BorderThickness = new Thickness(1),
            BorderBrush = AppResources.Get<Brush>(BoxBorderKey),
            Background = AppResources.Get<Brush>(BoxBackgroundKey),
            Child = grid
        };

        Canvas.SetLeft(border, stage.X);

        Canvas.SetTop(border, stage.Y);

        return border;
    }

    private static FrameworkElement CreateText(LineageGraph graph, string text, ulong address, bool isOperator)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = isOperator ? TextSize : TypeSize,
            FontWeight = isOperator ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var indent = new Thickness(isOperator ? 0 : CellPadding, 0, CellPadding, 0);

        if (address == 0)
        {
            block.Margin = indent;

            block.Foreground = AppResources.Get<Brush>(TypeKey);

            return block;
        }

        var link = new HyperlinkButton
        {
            Style = AppResources.Get<Style>(LinkStyleKey),
            Margin = indent,
            VerticalAlignment = VerticalAlignment.Center,
            Content = block
        };

        link.Click += (_, _) => graph.Open(address);

        return link;
    }

    private void UpdateCells()
    {
        foreach (var (stage, value) in Cells)
        {
            value.Text = stage.Value;

            ToolTipService.SetToolTip(value, stage.Value.Length > 0 ? stage.Value : null);
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
        }

        if (e.NewValue is LineageGraph graph)
        {
            graph.Changed += control.OnValuesChanged;
        }

        control.Build();
    }
}
