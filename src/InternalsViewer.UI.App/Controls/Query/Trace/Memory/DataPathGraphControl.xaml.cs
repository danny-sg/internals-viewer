using System;
using System.Collections.Generic;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.UI.App.Controls.Query.Plan;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.DataPath;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

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

    private const double NestedCalculatorWidth = 11;

    private const double NestedCalculatorHeight = 13;

    private const double ExpressionInset = 6;

    private const double ExpressionGap = 4;

    private const double ExpressionPadding = 2;

    private const double ExpressionRowHeight = 16;

    private const int CalculatorScale = 2;

    private const double BadgeGap = 4;

    private const double FaintOpacity = 0.35;

    private const string BoxBackgroundKey = "SolidBackgroundFillColorTertiaryBrush";

    private const string ValueBackgroundKey = "SolidBackgroundFillColorQuarternaryBrush";

    private const string HeaderBackgroundKey = "SolidBackgroundFillColorSecondaryBrush";

    private const string DetailKey = "TextFillColorSecondaryBrush";

    private const string ValueKey = "TextFillColorPrimaryBrush";

    private const string OutOfScopeKey = "SolidBackgroundFillColorBaseBrush";

    private const string LinkKey = "ControlStrongStrokeColorDefaultBrush";

    private const string LinkStyleKey = "DetailLinkStyle";

    private const string TagStyleKey = "TagTextBlockStyle";

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

    private static Image CreateCalculator(double width, double height)
        => new()
        {
            Width = width,
            Height = height,
            Stretch = Stretch.Fill,
            Margin = new Thickness(0, 0, BadgeGap, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Source = PlanIconParts.CreateCalculatorSource((int)width * CalculatorScale, (int)height * CalculatorScale)
        };

    private static TextBlock CreateExpression(PredicateText expression)
    {
        var text = CreateText(string.Empty, DetailSize);

        text.FontFamily = AppResources.Get<FontFamily>(MonospaceKey) ?? text.FontFamily;

        text.Margin = new Thickness(CellPadding, 0, CellPadding, 0);

        var fallback = AppResources.Get<Brush>(DetailKey) ?? text.Foreground;

        foreach (var token in expression.Tokens)
        {
            text.Inlines.Add(new Run { Text = token.Text, Foreground = PredicateTextBox.GetBrush(token.Type, fallback) });
        }

        ToolTipService.SetToolTip(text, expression.ToString());

        return text;
    }

    private static Border? CreateBadge(string kind, Color? colour)
    {
        if (kind.Length == 0)
        {
            return null;
        }

        var text = CreateText(kind, DetailSize);

        text.Style = AppResources.Get<Style>(TagStyleKey);

        text.Foreground = new SolidColorBrush(colour is null ? Colors.Black : Colors.White);

        return new Border
        {
            Background = colour is { } found ? new SolidColorBrush(found) : AppResources.Get<Brush>(HeaderBackgroundKey),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(BadgeGap, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = text
        };
    }

    private static Grid CreateHeader(string name, bool isCalculated, IEnumerable<FrameworkElement?> badges)
    {
        var header = new Grid
        {
            Padding = new Thickness(CellPadding, 0, CellPadding, 0),
            Background = AppResources.Get<Brush>(HeaderBackgroundKey),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            }
        };

        if (isCalculated)
        {
            header.Children.Add(CreateCalculator(CalculatorWidth, CalculatorHeight));
        }

        var text = CreateText(name, TextSize, true);

        Grid.SetColumn(text, 1);

        header.Children.Add(text);

        foreach (var badge in badges)
        {
            if (badge is null)
            {
                continue;
            }

            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(badge, header.ColumnDefinitions.Count - 1);

            header.Children.Add(badge);
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
                AddArrow(graph,
                         stage.X,
                         GetMiddle(stage.Y),
                         consumer.X + DataPathGraph.BoxWidth,
                         GetMiddle(consumer.Y),
                         stage.IsBuild,
                         !stage.IsSeen);
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
                         false,
                         false);
            }
        }

        foreach (var stage in graph.Stages)
        {
            var source = CreateSource(graph, stage.Source, stage.SourceAddress);

            var badges = new[]
            {
                CreateBadge(stage.Uses, DataPathText.UseColour),
                CreateBadge(stage.Kind, PlanOutputLinkText.GetReferenceColour(stage.Kind))
            };

            var expression = stage.Field.Definition is { } definition ? DataPathText.GetExpression(definition) : null;

            var nested = stage.IsComputed ? expression : null;

            List<FrameworkElement> lines = nested is not null ? [source]
                : expression is not null ? [source, CreateExpression(expression)]
                : [source, CreateDetail(stage.Detail)];

            Surface.Children.Add(CreateBox(new BoxContent(CreateHeader(stage.Name, expression is not null && nested is null, badges),
                                                          lines,
                                                          nested),
                                           () => stage.Value,
                                           () => stage.IsInScope,
                                           stage.X,
                                           stage.Y,
                                           stage.IsPointer));
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

            var expression = column.Definition is { } definition ? DataPathText.GetExpression(definition) : null;

            List<FrameworkElement> lines = expression is not null ? [CreateExpression(expression)] : [];

            Surface.Children.Add(CreateBox(new BoxContent(CreateHeader(column.Name, expression is not null, [type]), lines, null),
                                           () => column.Value,
                                           () => column.IsInScope,
                                           column.X,
                                           column.Y,
                                           false));
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

    private static Border CreateExpressionBox(PredicateText expression, Border valueBox)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(ExpressionRowHeight) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };

        var calculator = CreateCalculator(NestedCalculatorWidth, NestedCalculatorHeight);

        calculator.Margin = new Thickness(BadgeGap, 0, 0, 0);

        grid.Children.Add(calculator);

        var text = CreateExpression(expression);

        text.Margin = new Thickness(BadgeGap, 0, CellPadding, 0);

        Grid.SetColumn(text, 1);

        grid.Children.Add(text);

        Grid.SetRow(valueBox, 1);

        Grid.SetColumnSpan(valueBox, 2);

        grid.Children.Add(valueBox);

        return new Border
        {
            Margin = new Thickness(ExpressionInset, ExpressionGap, ExpressionInset, ExpressionGap),
            Padding = new Thickness(ExpressionPadding),
            BorderThickness = new Thickness(1),
            BorderBrush = AppResources.Get<Brush>(HeaderBackgroundKey),
            CornerRadius = new CornerRadius(2),
            Child = grid
        };
    }

    private FrameworkElement CreateBox(BoxContent content,
                                       Func<string> read,
                                       Func<bool> isInScope,
                                       double x,
                                       double y,
                                       bool isDashed)
    {
        var grid = new Grid
        {
            RowDefinitions = { new RowDefinition { Height = new GridLength(DataPathGraph.HeaderHeight) } }
        };

        grid.Children.Add(content.Header);

        foreach (var line in content.Lines)
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

        if (content.Expression is { } expression)
        {
            var nested = CreateExpressionBox(expression, valueBox);

            Grid.SetRow(nested, grid.RowDefinitions.Count);

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(DataPathGraph.LineHeight + DataPathGraph.ValueHeight) });

            grid.Children.Add(nested);
        }
        else
        {
            Grid.SetRow(valueBox, grid.RowDefinitions.Count);

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(DataPathGraph.ValueHeight) });

            grid.Children.Add(valueBox);
        }

        var lineCount = content.Lines.Count + (content.Expression is null ? 0 : 1);

        var height = DataPathGraph.HeaderHeight + lineCount * DataPathGraph.LineHeight + DataPathGraph.ValueHeight;

        var box = new Border
        {
            Width = DataPathGraph.BoxWidth,
            Height = height,
            BorderThickness = new Thickness(isDashed ? 0 : 1),
            BorderBrush = AppResources.Get<Brush>(HeaderBackgroundKey),
            Background = AppResources.Get<Brush>(BoxBackgroundKey),
            Child = grid
        };

        Cells.Add((read, isInScope, value, valueBox));

        FrameworkElement element = isDashed
            ? new Grid
            {
                Width = DataPathGraph.BoxWidth,
                Height = height,
                Children =
                {
                    box,
                    new Rectangle
                    {
                        Stroke = AppResources.Get<Brush>(LinkKey),
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { DashLength, DashGap }
                    }
                }
            }
            : box;

        Canvas.SetLeft(element, x);

        Canvas.SetTop(element, y);

        return element;
    }

    private void AddArrow(DataPathGraph graph,
                          double startX,
                          double startY,
                          double consumerRight,
                          double consumerY,
                          bool isDashed,
                          bool isFaint)
    {
        var brush = AppResources.Get<Brush>(LinkKey);

        var endX = consumerRight + ArrowLength;

        var middle = endX + (startX - endX) / 2;

        var line = new Polyline
        {
            Points = { Snap(startX, startY), Snap(middle, startY), Snap(middle, consumerY), Snap(endX, consumerY) },
            Stroke = brush,
            StrokeThickness = 1,
            Opacity = isFaint ? FaintOpacity : 1,
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
            Opacity = isFaint ? FaintOpacity : 1,
            Width = graph.Width,
            Height = graph.Height
        });
    }

    private void UpdateCells()
    {
        var outOfScope = AppResources.Get<Brush>(OutOfScopeKey);

        var inScopeBackground = AppResources.Get<Brush>(ValueBackgroundKey);

        var dimmed = AppResources.Get<Brush>(DetailKey);

        var normal = AppResources.Get<Brush>(ValueKey);

        foreach (var (read, isInScope, value, valueBox) in Cells)
        {
            var text = read();

            var inScope = isInScope();

            value.Text = text;

            value.Foreground = inScope ? normal : dimmed;

            valueBox.Background = inScope ? inScopeBackground : outOfScope;

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

    private sealed record BoxContent(Grid Header, IReadOnlyList<FrameworkElement> Lines, PredicateText? Expression);
}
