using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.UI.App.Controls.Query.Plan;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.DataPath;
using Microsoft.UI;
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

    private const double DetailSize = 11;

    private const double ArrowLength = 7;

    private const double ArrowWidth = 4;

    private const double DashLength = 4;

    private const double DashGap = 3;

    private const double CalculatorWidth = 14;

    private const double CalculatorHeight = 17;

    private const int CalculatorScale = 2;


    private const string BoxBackgroundKey = "SolidBackgroundFillColorQuarternaryBrush";

    private const string HeaderBackgroundKey = "SolidBackgroundFillColorSecondaryBrush";

    private const string DetailKey = "TextFillColorSecondaryBrush";

    private const string ValueKey = "TextFillColorPrimaryBrush";

    private const string OutOfScopeKey = "SolidBackgroundFillColorBaseBrush";

    private const string LinkKey = "ControlStrongStrokeColorDefaultBrush";

    private const string LinkStyleKey = "DetailLinkStyle";

    private const string TagStyleKey = "TagTextBlockStyle";

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

    private List<(Func<string> Read, Func<bool> IsInScope, TextBlock Value, Border Box)> Cells { get; } = [];

    private static Point Snap(double x, double y) => new(Math.Floor(x) + 0.5, Math.Floor(y) + 0.5);

    private static double GetMiddle(double y) => y + DataPathGraph.BoxHeight / 2;

    private static TextBlock CreateText(string text, double size, bool isBold = false)
        => new()
        {
            Text = text,
            FontSize = size,
            FontWeight = isBold ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

    private static Image CreateCalculator()
        => new()
        {
            Width = CalculatorWidth,
            Height = CalculatorHeight,
            Stretch = Stretch.Fill,
            Margin = new Thickness(0, 0, CellPadding, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Source = PlanIconParts.CreateCalculatorSource((int)CalculatorWidth * CalculatorScale,
                                                          (int)CalculatorHeight * CalculatorScale)
        };

    private static Border? CreateBadge(string kind)
    {
        if (kind.Length == 0)
        {
            return null;
        }

        var colour = PlanOutputLinkText.GetReferenceColour(kind);

        var text = CreateText(kind, DetailSize);

        text.Style = AppResources.Get<Style>(TagStyleKey);

        text.Foreground = new SolidColorBrush(colour is null ? Colors.Black : Colors.White);

        return new Border
        {
            Background = colour is { } found ? new SolidColorBrush(found) : AppResources.Get<Brush>(HeaderBackgroundKey),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(4, 1, 4, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = text
        };
    }

    private static Grid CreateHeader(string name, bool isCalculated, FrameworkElement? right)
    {
        var header = new Grid
        {
            Padding = new Thickness(CellPadding, 0, CellPadding, 0),
            Background = AppResources.Get<Brush>(HeaderBackgroundKey),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        header.Children.Add(CreateText(name, TextSize, true));

        if (isCalculated)
        {
            var calculator = CreateCalculator();

            Grid.SetColumn(calculator, 1);

            header.Children.Add(calculator);
        }

        if (right is not null)
        {
            Grid.SetColumn(right, 2);

            header.Children.Add(right);
        }

        return header;
    }

    private static FrameworkElement CreateSource(DataPathGraph graph, string text, ulong address)
    {
        var block = CreateText(text, TextSize, true);

        if (address == 0)
        {
            block.Margin = new Thickness(CellPadding, 0, CellPadding, 0);

            return block;
        }

        var link = new HyperlinkButton
        {
            Style = AppResources.Get<Style>(LinkStyleKey),
            Margin = new Thickness(CellPadding, 0, CellPadding, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = block
        };

        link.Click += (_, _) => graph.Open(address);

        return link;
    }

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
                AddArrow(graph, stage.X, GetMiddle(stage.Y), consumer.X + DataPathGraph.BoxWidth, GetMiddle(consumer.Y), stage.IsBuild);
            }
        }

        foreach (var column in graph.Columns)
        {
            foreach (var output in column.Outputs)
            {
                AddArrow(graph,
                         output.X,
                         GetMiddle(output.Y),
                         column.X + DataPathGraph.BoxWidth,
                         column.Y + column.Height / 2,
                         false);
            }
        }

        foreach (var stage in graph.Stages)
        {
            var source = CreateSource(graph, stage.Source, stage.SourceAddress);

            Surface.Children.Add(CreateBox(CreateHeader(stage.Name, stage.IsCalculated, CreateBadge(stage.Kind)),
                                           [source, CreateDetail(stage.Detail)],
                                           () => stage.Value,
                                           () => stage.IsInScope,
                                           stage.X,
                                           stage.Y));
        }

        foreach (var column in graph.Columns)
        {
            var type = new DataTypeTextBlock
            {
                DataType = SqlDataTypeFormat.GetDataType(column.Type),
                Precision = column.Type?.Precision ?? 0,
                DataScale = column.Type?.Scale ?? 0,
                Length = column.Type?.MaxLength ?? 0,
                VerticalAlignment = VerticalAlignment.Center
            };

            List<FrameworkElement> lines = column.Definition is { } definition ? [CreateDetail(DataPathPlan.GetText(definition))] : [];

            Surface.Children.Add(CreateBox(CreateHeader(column.Name, column.Expression is not null, type),
                                           lines,
                                           () => column.Value,
                                           () => column.IsInScope,
                                           column.X,
                                           column.Y));
        }

        UpdateCells();
    }

    private static TextBlock CreateDetail(string detail)
    {
        var text = CreateText(detail, DetailSize);

        text.Margin = new Thickness(CellPadding, 0, CellPadding, 0);

        text.Foreground = AppResources.Get<Brush>(DetailKey);

        ToolTipService.SetToolTip(text, detail.Length > 0 ? detail : null);

        return text;
    }

    private Border CreateBox(Grid header,
                             IReadOnlyList<FrameworkElement> lines,
                             Func<string> read,
                             Func<bool> isInScope,
                             double x,
                             double y)
    {
        var grid = new Grid
        {
            RowDefinitions = { new RowDefinition { Height = new GridLength(DataPathGraph.HeaderHeight) } }
        };

        grid.Children.Add(header);

        foreach (var line in lines)
        {
            Grid.SetRow(line, grid.RowDefinitions.Count);

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(DataPathGraph.LineHeight) });

            grid.Children.Add(line);
        }

        var value = CreateText(string.Empty, TextSize);

        value.Margin = new Thickness(CellPadding, 0, CellPadding, 0);

        var valueBox = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = AppResources.Get<Brush>(HeaderBackgroundKey),
            Child = value
        };

        Grid.SetRow(valueBox, grid.RowDefinitions.Count);

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(DataPathGraph.ValueHeight) });

        grid.Children.Add(valueBox);

        var box = new Border
        {
            Width = DataPathGraph.BoxWidth,
            Height = DataPathGraph.HeaderHeight + lines.Count * DataPathGraph.LineHeight + DataPathGraph.ValueHeight,
            BorderThickness = new Thickness(1),
            BorderBrush = AppResources.Get<Brush>(HeaderBackgroundKey),
            Background = AppResources.Get<Brush>(BoxBackgroundKey),
            Child = grid
        };

        Canvas.SetLeft(box, x);

        Canvas.SetTop(box, y);

        Cells.Add((read, isInScope, value, valueBox));

        return box;
    }

    private void AddArrow(DataPathGraph graph, double startX, double startY, double consumerRight, double consumerY, bool isDashed)
    {
        var brush = AppResources.Get<Brush>(LinkKey);

        var endX = consumerRight + ArrowLength;

        var middle = endX + (startX - endX) / 2;

        var line = new Polyline
        {
            Points = { Snap(startX, startY), Snap(middle, startY), Snap(middle, consumerY), Snap(endX, consumerY) },
            Stroke = brush,
            StrokeThickness = 1,
            Width = graph.Width,
            Height = graph.Height
        };

        if (isDashed)
        {
            line.StrokeDashArray = new DoubleCollection { DashLength, DashGap };
        }

        Surface.Children.Add(line);

        Surface.Children.Add(new Polygon
        {
            Points =
            {
                Snap(consumerRight, consumerY),
                Snap(endX, consumerY - ArrowWidth),
                Snap(endX, consumerY + ArrowWidth)
            },
            Fill = brush,
            Width = graph.Width,
            Height = graph.Height
        });
    }

    private void UpdateCells()
    {
        var outOfScope = AppResources.Get<Brush>(OutOfScopeKey);

        var dimmed = AppResources.Get<Brush>(DetailKey);

        var normal = AppResources.Get<Brush>(ValueKey);

        foreach (var (read, isInScope, value, valueBox) in Cells)
        {
            var text = read();

            var inScope = isInScope();

            value.Text = text;

            value.Foreground = inScope ? normal : dimmed;

            valueBox.Background = inScope ? null : outOfScope;

            ToolTipService.SetToolTip(value, text.Length > 0 ? text : null);
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
