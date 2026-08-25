using System.Collections.Generic;
using System.Globalization;
using System.Text;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Plan;

public sealed partial class PlanNodeControl : UserControl
{
    private const byte ActiveBackgroundAlpha = 50;

    // A faint divider so adjacent frames read apart even at 3-4px row heights.
    private static readonly SolidColorBrush IcicleDivider = new(Color.FromArgb(70, 0, 0, 0));

    public static readonly DependencyProperty NodeProperty =
        DependencyProperty.Register(nameof(Node), typeof(PlanNode), typeof(PlanNodeControl),
            new PropertyMetadata(null, OnNodeChanged));

    public PlanNode? Node
    {
        get => (PlanNode?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public static readonly DependencyProperty CostPercentProperty =
        DependencyProperty.Register(nameof(CostPercent), typeof(double), typeof(PlanNodeControl),
            new PropertyMetadata(-1d, OnNodeChanged));

    public double CostPercent
    {
        get => (double)GetValue(CostPercentProperty);
        set => SetValue(CostPercentProperty, value);
    }

    public static readonly DependencyProperty IcicleSegmentsProperty =
        DependencyProperty.Register(nameof(IcicleSegments), typeof(IReadOnlyList<IcicleSegment>), typeof(PlanNodeControl),
            new PropertyMetadata(null, OnIcicleSegmentsChanged));

    /// <summary>The call-stack icicle for this operator (see <see cref="OperatorIcicle"/>); empty when it has none</summary>
    public IReadOnlyList<IcicleSegment>? IcicleSegments
    {
        get => (IReadOnlyList<IcicleSegment>?)GetValue(IcicleSegmentsProperty);
        set => SetValue(IcicleSegmentsProperty, value);
    }

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(PlanNodeControl),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(PlanNodeControl),
            new PropertyMetadata(false, OnIsActiveChanged));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public PlanNodeControl()
    {
        InitializeComponent();
    }

    public SvgImageSource? IconSource => Node is null ? null : new SvgImageSource(PlanIconResolver.Resolve(Node));

    public string OperatorName => Node?.PhysicalOperator ?? string.Empty;

    public string DetailText => Node is null ? string.Empty : FormatObject(Node);

    public Visibility DetailVisibility
        => string.IsNullOrEmpty(DetailText) ? Visibility.Collapsed : Visibility.Visible;

    public string CostText => CostPercent < 0 ? string.Empty : $"Cost: {CostPercent:P0}";

    public Visibility CostVisibility
        => CostPercent < 0 ? Visibility.Collapsed : Visibility.Visible;

    public string ToolTipText => Node is null ? string.Empty : BuildToolTip(Node);

    private void RenderIcicle()
    {
        IcicleCanvas.Children.Clear();

        var segments = IcicleSegments;

        if (segments is null || segments.Count == 0)
        {
            IcicleCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        IcicleCanvas.Visibility = Visibility.Visible;

        foreach (var segment in segments)
        {
            var rectangle = new Rectangle
            {
                Width = segment.Width,
                Height = segment.Height,
                Fill = ParseBrush(segment.Colour),
                Stroke = IcicleDivider,
                StrokeThickness = 0.3
            };

            Canvas.SetLeft(rectangle, segment.X);
            Canvas.SetTop(rectangle, segment.Y);

            ToolTipService.SetToolTip(rectangle, segment.Symbol);

            IcicleCanvas.Children.Add(rectangle);
        }
    }

    private static SolidColorBrush ParseBrush(string hex)
    {
        hex = hex.TrimStart('#');

        if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, null, out var rgb))
        {
            return new SolidColorBrush(Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        }

        return new SolidColorBrush(Colors.Gray);
    }

    private void UpdateStateVisual()
    {
        if (IsActive && Node is { } node)
        {
            var type = EventColourProvider.GetOperatorColour(node).ToWindowsColor();

            NodeImageBorder.Background = new SolidColorBrush(Color.FromArgb(ActiveBackgroundAlpha, type.R, type.G, type.B));
        }
        else
        {
            NodeImageBorder.Background = null;
        }

        if (IsSelected && Node is { } selectedNode)
        {
            var type = EventColourProvider.GetOperatorColour(selectedNode).ToWindowsColor();

            NodeImageBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(255, type.R, type.G, type.B));
        }
        else
        {
            NodeImageBorder.BorderBrush = null;
        }
    }

    private static string FormatObject(PlanNode node)
    {
        var isIo = OperatorClassifier.IsDataAccess(node);

        if(isIo)
        {
            var table = Trim(node.Table);

            if (string.IsNullOrEmpty(table))
            {
                return string.Empty;
            }

            var index = Trim(node.Index);

            return string.IsNullOrEmpty(index) ? table : $"{table}.{index}";
        }

        if (node.PhysicalOperator == node.LogicalOperator)
        {
            return string.Empty;
        }

        return node.LogicalOperator;
    }

    private string BuildToolTip(PlanNode node)
    {
        var builder = new StringBuilder();

        builder.Append(node.PhysicalOperator);

        if (!string.IsNullOrEmpty(node.LogicalOperator)
            && node.LogicalOperator != node.PhysicalOperator)
        {
            builder.Append(" (").Append(node.LogicalOperator).Append(')');
        }

        var schema = Trim(node.Schema);
        var table = Trim(node.Table);

        if (!string.IsNullOrEmpty(table))
        {
            builder.Append("\nObject: ");

            if (!string.IsNullOrEmpty(schema))
            {
                builder.Append(schema).Append('.');
            }

            builder.Append(table);

            var index = Trim(node.Index);

            if (!string.IsNullOrEmpty(index))
            {
                builder.Append('.').Append(index);
            }
        }

        if (node.EstimatedCost is { } cost)
        {
            builder.Append("\nEstimated Subtree Cost: ").Append(cost.ToString("0.######"));
        }

        if (CostPercent >= 0)
        {
            builder.Append("\nCost: ").Append(CostPercent.ToString("P1"));
        }

        if (!node.IsStatement)
        {
            builder.Append("\nNode ID: ").Append(node.NodeId);
        }

        return builder.ToString();
    }

    private static string Trim(string? value) => value?.Trim('[', ']') ?? string.Empty;

    private static void OnNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).Bindings.Update();

    private static void OnIcicleSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).RenderIcicle();

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).UpdateStateVisual();

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).UpdateStateVisual();
}
