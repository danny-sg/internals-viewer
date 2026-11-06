using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Plan;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Query.Plan;

public sealed partial class PlanNodeControl : UserControl
{
    private const byte ActiveBackgroundAlpha = 50;

    // A faint divider so adjacent frames read apart even at 3-4px row heights.
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

    public static readonly DependencyProperty AreAnnotationsVisibleProperty =
        DependencyProperty.Register(nameof(AreAnnotationsVisible), typeof(bool), typeof(PlanNodeControl),
            new PropertyMetadata(false, OnAnnotationsChanged));

    public bool AreAnnotationsVisible
    {
        get => (bool)GetValue(AreAnnotationsVisibleProperty);
        set => SetValue(AreAnnotationsVisibleProperty, value);
    }

    public PlanNodeControl()
    {
        InitializeComponent();
    }

    public const double AnnotationColumnWidth = 104;

    public const double IconTop = 4;

    private void RefreshAnnotations()
    {
        var annotations = AreAnnotationsVisible && Node is { } node ? PlanNodeAnnotations.For(node) : [];

        AnnotationBadges.ItemsSource = annotations;

        AnnotationColumn.Width = annotations.Count > 0 ? new GridLength(AnnotationColumnWidth) : new GridLength(0);

        AnnotationBadges.Visibility = annotations.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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

    private Dictionary<(string Part, Color Colour), PartLayer> PartLayers { get; } = [];

    private string CounterToolTip { get; set; } = string.Empty;

    public void ShowCounters(IEnumerable<PlanCounter> counters)
    {
        var shown = counters.ToList();

        var toolTip = string.Join(Environment.NewLine, shown.Select(c => $"{c.Name}: {c.Count:N0}"));

        if (toolTip == CounterToolTip)
        {
            return;
        }

        CounterToolTip = toolTip;

        if (shown.Count == 0)
        {
            CounterBadge.Visibility = Visibility.Collapsed;

            return;
        }

        CounterText.Text = FormatCount(shown[0].Count);

        CounterBadge.Background = new SolidColorBrush(shown[0].Colour);

        ToolTipService.SetToolTip(CounterBadge, toolTip);

        CounterBadge.Visibility = Visibility.Visible;
    }

    public void ShowParts(IEnumerable<PlanPartActivity> parts, TimeSpan fade)
    {
        if (Node is not { } node)
        {
            return;
        }

        var icon = PlanIconResolver.GetAssetName(node);

        var lit = new HashSet<(string Part, Color Colour)>();

        foreach (var activity in parts)
        {
            foreach (var part in PlanIconParts.GetParts(icon, activity.Part))
            {
                var key = (part, activity.Colour);

                lit.Add(key);

                if (!PartLayers.ContainsKey(key))
                {
                    PartLayers[key] = AddPartLayer(icon, part, activity.Colour);
                }
            }
        }

        foreach (var (key, layer) in PartLayers)
        {
            if (lit.Contains(key))
            {
                layer.Highlight.Light();
            }
            else
            {
                layer.Highlight.Fade(fade);
            }
        }
    }

    private PartLayer AddPartLayer(string icon, string part, Color colour)
    {
        var order = PlanIconParts.GetOrder(icon, part);

        var image = new Image
        {
            Width = NodeImage.Width,
            Height = NodeImage.Height,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };

        var position = 1 + PartLayers.Values.Count(l => l.Order <= order);

        IconLayers.Children.Insert(Math.Min(position, IconLayers.Children.Count), image);

        _ = LoadPartLayerAsync(image, icon, part, colour);

        return new PartLayer(order, new PlanHighlight(image));
    }

    private static async Task LoadPartLayerAsync(Image image, string icon, string part, Color colour)
        => image.Source = await PlanIconParts.GetOverlayAsync(icon, part, colour);

    private static string FormatCount(int count) => count switch
    {
        < 10_000 => count.ToString("N0", CultureInfo.CurrentCulture),
        < 1_000_000 => $"{count / 1_000:N0}K",
        _ => $"{count / 1_000_000d:0.#}M"
    };

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
    {
        var control = (PlanNodeControl)d;

        control.Bindings.Update();

        control.RefreshAnnotations();
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).UpdateStateVisual();

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).UpdateStateVisual();

    private static void OnAnnotationsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).RefreshAnnotations();

    private sealed record PartLayer(int Order, PlanHighlight Highlight);
}
