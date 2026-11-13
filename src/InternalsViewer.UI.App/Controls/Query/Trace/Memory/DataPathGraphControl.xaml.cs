using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.DataPath;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Memory;

public sealed partial class DataPathGraphControl : UserControl
{
    private const double CellPadding = 8;

    private const double TextSize = 12;

    private const double TypeSize = 11;

    private const double ArrowLength = 7;

    private const double ArrowWidth = 4;

    private const double DashLength = 4;

    private const double DashGap = 3;

    private const string BoxBorderKey = "CardStrokeColorDefaultBrush";

    private const string BoxBackgroundKey = "SolidBackgroundFillColorBaseBrush";

    private const string HeaderBackgroundKey = "SolidBackgroundFillColorSecondaryBrush";

    private const string TypeKey = "TextFillColorSecondaryBrush";

    private const string LinkKey = "ControlStrongStrokeColorDefaultBrush";

    private const string LinkStyleKey = "DetailLinkStyle";

    private const string SqlTypeKey = "SqlKeywordBrush";

    private const string MonospaceKey = "MonospaceFontFamily";

    public static readonly DependencyProperty GraphProperty =
        DependencyProperty.Register(nameof(Graph),
                                    typeof(DataPathGraph),
                                    typeof(DataPathGraphControl),
                                    new PropertyMetadata(null, OnGraphChanged));

    public DataPathGraph? Graph
    {
        get => (DataPathGraph?)GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    public DataPathGraphControl()
    {
        InitializeComponent();
    }

    private List<(DataPathStage Stage, TextBlock Value)> Cells { get; } = [];

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
                AddArrow(graph, stage, consumer.X + DataPathGraph.BoxWidth, consumer.Y, stage.IsBuild);
            }
        }

        foreach (var column in graph.Columns)
        {
            if (column.Output is { } output)
            {
                AddArrow(graph, output, column.X + DataPathGraph.ColumnWidth, column.Y, false);
            }
        }

        foreach (var stage in graph.Stages)
        {
            Surface.Children.Add(CreateBox(graph, stage));
        }

        foreach (var column in graph.Columns)
        {
            Surface.Children.Add(CreateColumn(column));
        }

        UpdateCells();
    }

    private static Border CreateColumn(DataPathColumn column)
    {
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(DataPathGraph.OperatorHeight) },
                new RowDefinition { Height = new GridLength(DataPathGraph.LineHeight) }
            }
        };

        grid.Children.Add(new Border
        {
            Padding = new Thickness(CellPadding, 0, CellPadding, 0),
            Background = AppResources.Get<Brush>(HeaderBackgroundKey),
            Child = new TextBlock
            {
                Text = column.Name,
                FontSize = TextSize,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        });

        var type = new TextBlock
        {
            Text = column.Type,
            FontSize = TypeSize,
            FontFamily = AppResources.Get<FontFamily>(MonospaceKey),
            Foreground = AppResources.Get<Brush>(SqlTypeKey),
            Margin = new Thickness(CellPadding, 0, CellPadding, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        Grid.SetRow(type, 1);

        grid.Children.Add(type);

        var border = new Border
        {
            Width = DataPathGraph.ColumnWidth,
            Height = DataPathGraph.BoxHeight,
            BorderThickness = new Thickness(1),
            BorderBrush = AppResources.Get<Brush>(BoxBorderKey),
            Background = AppResources.Get<Brush>(BoxBackgroundKey),
            Child = grid
        };

        Canvas.SetLeft(border, column.X);

        Canvas.SetTop(border, column.Y);

        return border;
    }

    private void AddArrow(DataPathGraph graph, DataPathStage input, double consumerRight, double consumerY, bool isDashed)
    {
        var brush = AppResources.Get<Brush>(LinkKey);

        var startX = input.X;

        var startY = input.Y + DataPathGraph.BoxHeight / 2;

        var endX = consumerRight + ArrowLength;

        var endY = consumerY + DataPathGraph.BoxHeight / 2;

        var middle = endX + (startX - endX) / 2;

        var line = new Polyline
        {
            Points = { Snap(startX, startY), Snap(middle, startY), Snap(middle, endY), Snap(endX, endY) },
            Stroke = brush,
            StrokeThickness = 1,
            Width = graph.Width,
            Height = graph.Height
        };

        if (isDashed)
        {
            line.StrokeDashArray = [DashLength, DashGap];
        }

        Surface.Children.Add(line);

        Surface.Children.Add(new Polygon
        {
            Points =
            {
                Snap(consumerRight, endY),
                Snap(endX, endY - ArrowWidth),
                Snap(endX, endY + ArrowWidth)
            },
            Fill = brush,
            Width = graph.Width,
            Height = graph.Height
        });
    }

    private Border CreateBox(DataPathGraph graph, DataPathStage stage)
    {
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(DataPathGraph.OperatorHeight) },
                new RowDefinition { Height = new GridLength(DataPathGraph.LineHeight) },
                new RowDefinition { Height = new GridLength(DataPathGraph.LineHeight) }
            }
        };

        var iterator = stage.Stage is { } output ? output.Iterator != 0 ? output.Iterator : output.PlanObject : 0;

        var header = new Border
        {
            Padding = new Thickness(CellPadding, 0, CellPadding, 0),
            Background = AppResources.Get<Brush>(HeaderBackgroundKey),
            Child = CreateText(graph, stage.Operator, iterator, true)
        };

        grid.Children.Add(header);

        var type = CreateText(graph, stage.Type, stage.Stage?.Address ?? 0, false);

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
            Width = DataPathGraph.BoxWidth,
            Height = DataPathGraph.BoxHeight,
            BorderThickness = new Thickness(1),
            BorderBrush = AppResources.Get<Brush>(BoxBorderKey),
            Background = AppResources.Get<Brush>(BoxBackgroundKey),
            Child = grid
        };

        Canvas.SetLeft(border, stage.X);

        Canvas.SetTop(border, stage.Y);

        return border;
    }

    private static FrameworkElement CreateText(DataPathGraph graph, string text, ulong address, bool isOperator)
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
        var control = (DataPathGraphControl)d;

        if (e.OldValue is DataPathGraph old)
        {
            old.Changed -= control.OnValuesChanged;
        }

        if (e.NewValue is DataPathGraph graph)
        {
            graph.Changed += control.OnValuesChanged;
        }

        control.Build();
    }
}
