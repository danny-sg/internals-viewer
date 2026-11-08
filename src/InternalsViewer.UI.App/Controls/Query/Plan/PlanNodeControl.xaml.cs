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
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI;
using Windows.Foundation;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Query.Plan;

public sealed partial class PlanNodeControl : UserControl
{
    private const byte ActiveBackgroundAlpha = 50;

    private const double IconSize = 32;

    private const double IndicatorWidth = 16;

    private const int BaseLayerCount = 1;

    private const double FractionSteps = 8;

    private const double PageFontSize = 12;

    private const double MinimumOperatorFontSize = 10;

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

        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    public event EventHandler<PlanPage>? PageOpenRequested;

    public const double AnnotationColumnWidth = 104;

    public const double IconTop = 4;

    private void RefreshAnnotations()
    {
        var annotations = AreAnnotationsVisible && Node is { } node ? PlanNodeAnnotations.For(node) : [];

        AnnotationBadges.ItemsSource = annotations;

        AnnotationColumn.Width = annotations.Count > 0 ? new GridLength(AnnotationColumnWidth) : new GridLength(0);

        AnnotationBadges.Visibility = annotations.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public SvgImageSource? IconSource => Node is null ? null : CreatedIconSource ??= CreateIconSource(Node);

    public string OperatorName => Node?.PhysicalOperator ?? string.Empty;

    public string DetailText => Node is null ? string.Empty : FormatObject(Node);

    public Visibility DetailVisibility
        => string.IsNullOrEmpty(DetailText) ? Visibility.Collapsed : Visibility.Visible;

    public string CostText => CostPercent < 0 ? string.Empty : $"Cost: {CostPercent:P0}";

    public Visibility CostVisibility
        => CostPercent < 0 ? Visibility.Collapsed : Visibility.Visible;

    public string ToolTipText => Node is null ? string.Empty : BuildToolTip(Node);

    private Dictionary<(string Icon, string Part, Color Colour, double? Fraction), PartLayer> PartLayers { get; } = [];

    private string CounterToolTip { get; set; } = string.Empty;

    private double RenderScale { get; set; } = 1;

    private double Zoom { get; set; } = 1;

    private PlanPage? CurrentPage { get; set; }

    private double RasterScale { get; set; } = 1;

    private bool IsGreyed { get; set; }

    private SvgImageSource? CreatedIconSource { get; set; }

    public void SetRenderScale(double scale, double zoom)
    {
        RenderScale = scale;

        Zoom = zoom;

        UpdatePageLink();

        if (GetPixels(IconSize, scale) != GetPixels(IconSize, RasterScale)
            || GetPixels(IndicatorWidth, scale) != GetPixels(IndicatorWidth, RasterScale))
        {
            RasterScale = scale;

            RefreshSources();
        }

        SnapToPixels();
    }

    private void RefreshSources()
    {
        if (CreatedIconSource is not null && Node is { } node)
        {
            CreatedIconSource = CreateIconSource(node);

            NodeImage.Source = CreatedIconSource;
        }

        foreach (var layer in PartLayers.Values.Where(l => l.Svg is not null))
        {
            layer.Image.Source = CreateOverlaySource(layer.Svg!, layer.Image.Width);
        }
    }

    private static double GetPixels(double size, double scale) => Math.Max(1, Math.Round(size * scale));

    private static HorizontalAlignment GetAlignment(string icon)
        => icon == PlanIconParts.PageIcon ? HorizontalAlignment.Right : HorizontalAlignment.Center;

    public void SetGreyed(bool isGreyed)
    {
        if (isGreyed == IsGreyed)
        {
            return;
        }

        IsGreyed = isGreyed;

        if (CreatedIconSource is not null && Node is { } node)
        {
            CreatedIconSource = CreateIconSource(node);

            NodeImage.Source = CreatedIconSource;
        }
    }

    private SvgImageSource CreateIconSource(PlanNode node)
    {
        if (IsGreyed && PlanIconParts.GetInactiveIcon(PlanIconResolver.GetAssetName(node)) is { } inactive)
        {
            return CreateOverlaySource(inactive, IconSize);
        }

        var source = new SvgImageSource(PlanIconResolver.Resolve(node));

        SetRasterSize(source, IconSize);

        return source;
    }

    public void ShowPage(PlanPage? page)
    {
        if (page == CurrentPage)
        {
            return;
        }

        CurrentPage = page;

        PageText.Text = page?.Address ?? string.Empty;

        UpdatePageLink();
    }

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

    public void ShowParts(IEnumerable<PlanPartActivity> parts)
    {
        if (Node is not { } node)
        {
            return;
        }

        var icon = PlanIconResolver.GetAssetName(node);

        var hasPage = PlanIconParts.HasPage(icon);

        var lit = new HashSet<(string Icon, string Part, Color Colour, double? Fraction)>();

        foreach (var activity in parts)
        {
            var fraction = activity.Fraction is { } value ? Math.Round(value * FractionSteps) / FractionSteps : (double?)null;

            var iconParts = PlanIconParts.GetParts(icon, activity.Part)
                                         .Where(p => !node.IsStatement || p != PlanIconParts.ActivePart)
                                         .Select(p => (Icon: icon, Part: p));

            var pageParts = hasPage
                ? PlanIconParts.GetParts(PlanIconParts.PageIcon, activity.Part, owner: icon)
                               .Select(p => (Icon: PlanIconParts.PageIcon, Part: p))
                : [];

            foreach (var (partIcon, part) in iconParts.Concat(pageParts))
            {
                var key = (partIcon, part, activity.Colour, fraction);

                lit.Add(key);

                if (!PartLayers.ContainsKey(key))
                {
                    PartLayers[key] = AddPartLayer(partIcon, part, activity.Colour, fraction, activity.Rank);
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
                layer.Highlight.Fade();
            }
        }
    }

    private PartLayer AddPartLayer(string icon, string part, Color colour, double? fraction, int rank)
    {
        var alignment = GetAlignment(icon);

        var order = PlanIconParts.GetOrder(icon, part);

        var image = new Image
        {
            Width = alignment == HorizontalAlignment.Center ? IconSize : IndicatorWidth,
            Height = IconSize,
            Stretch = Stretch.Fill,
            HorizontalAlignment = alignment,
            IsHitTestVisible = false
        };

        var position = BaseLayerCount
                       + PartLayers.Values.Count(l => l.Alignment < alignment
                                                      || (l.Alignment == alignment
                                                          && (l.Order < order || (l.Order == order && l.Rank <= rank))));

        IconLayers.Children.Insert(Math.Min(position, IconLayers.Children.Count), image);

        SnapToPixels(image);

        var svg = PlanIconParts.GetOverlay(icon, part, colour, fraction);

        if (svg is not null)
        {
            image.Source = CreateOverlaySource(svg, image.Width);
        }

        return new PartLayer(order, rank, alignment, image, new PlanHighlight(image), svg);
    }

    private SvgImageSource CreateOverlaySource(string svg, double width)
    {
        var source = new SvgImageSource();

        SetRasterSize(source, width);

        _ = PlanIconParts.LoadAsync(source, svg);

        return source;
    }

    private void SetRasterSize(SvgImageSource source, double width)
    {
        source.RasterizePixelWidth = GetPixels(width, RasterScale);

        source.RasterizePixelHeight = GetPixels(IconSize, RasterScale);
    }

    private void SnapToPixels()
    {
        foreach (var image in IconLayers.Children.OfType<Image>())
        {
            SnapToPixels(image);
        }
    }

    private void SnapToPixels(Image image)
    {
        if (!IsLoaded || XamlRoot is not { } root)
        {
            return;
        }

        if (image.RenderTransform is not CompositeTransform snap)
        {
            snap = new CompositeTransform();

            image.RenderTransform = snap;
        }

        var left = image.HorizontalAlignment == HorizontalAlignment.Right
            ? IconLayers.Width - image.Width
            : (IconLayers.Width - image.Width) / 2;

        var origin = IconLayers.TransformToVisual(null).TransformPoint(new Point(left, 0));

        snap.ScaleX = GetPixels(image.Width, RasterScale) / (image.Width * RenderScale);

        snap.ScaleY = GetPixels(IconSize, RasterScale) / (IconSize * RenderScale);

        snap.TranslateX = GetPixelSnap(origin.X * root.RasterizationScale);

        snap.TranslateY = GetPixelSnap(origin.Y * root.RasterizationScale);
    }

    private double GetPixelSnap(double pixel) => (Math.Round(pixel) - pixel) / RenderScale;

    private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args) => SnapToPixels();

    private void OnPageLinkClick(Hyperlink sender, HyperlinkClickEventArgs args)
    {
        if (CurrentPage is { } page)
        {
            PageOpenRequested?.Invoke(this, page);
        }
    }

    private void UpdatePageLink()
    {
        PageLink.FontSize = PageFontSize / Zoom;

        PageLink.Visibility = CurrentPage is not null && OperatorNameText.FontSize * Zoom >= MinimumOperatorFontSize
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

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

        if (e.Property == NodeProperty)
        {
            control.CreatedIconSource = null;
        }

        control.Bindings.Update();

        control.RefreshAnnotations();
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).UpdateStateVisual();

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).UpdateStateVisual();

    private static void OnAnnotationsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PlanNodeControl)d).RefreshAnnotations();

    private sealed record PartLayer(int Order, int Rank, HorizontalAlignment Alignment, Image Image, PlanHighlight Highlight, string? Svg);
}
