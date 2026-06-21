using System.Text;
using InternalsViewer.Replay.Plans;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Plan;

public sealed partial class PlanNodeControl : UserControl
{
    public PlanNodeControl()
    {
        InitializeComponent();
    }

    public PlanNode? Node
    {
        get => (PlanNode?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public static readonly DependencyProperty NodeProperty =
        DependencyProperty.Register(nameof(Node), typeof(PlanNode), typeof(PlanNodeControl),
            new PropertyMetadata(null, OnNodeChanged));

    /// <summary>Operator cost as a fraction of the whole plan (0-1). Negative hides the label.</summary>
    public double CostPercent
    {
        get => (double)GetValue(CostPercentProperty);
        set => SetValue(CostPercentProperty, value);
    }

    public static readonly DependencyProperty CostPercentProperty =
        DependencyProperty.Register(nameof(CostPercent), typeof(double), typeof(PlanNodeControl),
            new PropertyMetadata(-1d, OnNodeChanged));

    private static void OnNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).Bindings.Update();

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(PlanNodeControl),
            new PropertyMetadata(false, OnIsSelectedChanged));

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).UpdateSelectionVisual();

    private void UpdateSelectionVisual()
    {
        if (IsSelected)
        {
            var accent = (Color)Application.Current.Resources["SystemAccentColor"];

            NodeBorder.BorderBrush = new SolidColorBrush(accent);
            NodeBorder.BorderThickness = new Thickness(1);
            NodeBorder.Background = new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B));
        }
        else
        {
            NodeBorder.BorderThickness = new Thickness(0);
            NodeBorder.Background = null;
        }
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

    private static string FormatObject(PlanNode node)
    {
        var table = Trim(node.Table);

        if (string.IsNullOrEmpty(table))
        {
            return string.Empty;
        }

        var index = Trim(node.Index);

        return string.IsNullOrEmpty(index) ? table : $"{table}.{index}";
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
}
