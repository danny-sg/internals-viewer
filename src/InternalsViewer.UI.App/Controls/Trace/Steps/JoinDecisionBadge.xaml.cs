using InternalsViewer.Execution.AccessPaths.Joins;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace InternalsViewer.UI.App.Controls.Trace.Steps;

/// <summary>
/// Shows what a join found on each side next to what its join type requires, and the outcome that follows
/// </summary>
public sealed partial class JoinDecisionBadge : UserControl
{
    private static readonly SolidColorBrush FoundBrush = new(Windows.UI.Color.FromArgb(255, 15, 123, 15));

    private static readonly SolidColorBrush MissingBrush = new(Windows.UI.Color.FromArgb(255, 168, 46, 46));

    private static readonly SolidColorBrush AnyBrush = new(Windows.UI.Color.FromArgb(255, 214, 214, 214));

    private static readonly SolidColorBrush ChipBackground = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));

    private static readonly SolidColorBrush ChipForeground = new(Windows.UI.Color.FromArgb(255, 32, 32, 32));

    public static readonly DependencyProperty DecisionProperty =
        DependencyProperty.Register(nameof(Decision),
                                    typeof(object),
                                    typeof(JoinDecisionBadge),
                                    new PropertyMetadata(null, OnDecisionChanged));

    public object? Decision
    {
        get => GetValue(DecisionProperty);
        set => SetValue(DecisionProperty, value);
    }

    public static readonly DependencyProperty IsRuleOnlyProperty =
        DependencyProperty.Register(nameof(IsRuleOnly),
                                    typeof(bool),
                                    typeof(JoinDecisionBadge),
                                    new PropertyMetadata(false, OnDecisionChanged));

    /// <summary>
    /// Shows only what the join requires, leaving out what was found on a given comparison
    /// </summary>
    public bool IsRuleOnly
    {
        get => (bool)GetValue(IsRuleOnlyProperty);
        set => SetValue(IsRuleOnlyProperty, value);
    }

    public JoinDecisionBadge()
    {
        InitializeComponent();
    }

    private static void OnDecisionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((JoinDecisionBadge)d).Rebuild();
    }

    private void Rebuild()
    {
        Root.Children.Clear();

        if (Decision is not JoinDecision decision)
        {
            return;
        }

        if (IsRuleOnly)
        {
            Root.Children.Add(Label(decision.JoinName));
            Root.Children.Add(RuleChip("Outer", decision.OuterRule, false, 1, showLabel: true));
            Root.Children.Add(RuleChip("Inner", decision.InnerRule, true, 1, showLabel: true));

            return;
        }

        Root.Children.Add(SideChip("Outer", decision.HasOuter, false));
        Root.Children.Add(SideChip("Inner", decision.HasInner, true));

        Root.Children.Add(Label("Rule:", 6));
        Root.Children.Add(RuleChip("Outer", decision.OuterRule, false));
        Root.Children.Add(RuleChip("Inner", decision.InnerRule, true));

        Root.Children.Add(Outcome(decision.IsEmitted));
    }

    private static TextBlock Label(string text, double leftMargin = 0)
        => new()
        {
            Text = text,
            FontSize = 11,
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(leftMargin, 0, 4, 0)
        };

    private static Border SideChip(string side, bool hasRow, bool isInner)
    {
        var glyph = new TextBlock
        {
            Text = hasRow ? "✓" : "✕",
            Foreground = hasRow ? FoundBrush : MissingBrush,
            FontSize = 11,
            Margin = new Thickness(4, 0, 0, 0)
        };

        var chip = Chip(side, glyph, 1);

        ToolTipService.SetToolTip(chip,
                                  hasRow
                                      ? $"A {Label(isInner)} row was found for this key"
                                      : $"No {Label(isInner)} row was found for this key");

        return chip;
    }

    private static Border RuleChip(string side, JoinSlotRule rule, bool isInner, double opacity = 0.55, bool showLabel = false)
    {
        FrameworkElement glyph = rule switch
        {
            JoinSlotRule.Present => new TextBlock
            {
                Text = "✓",
                Foreground = FoundBrush,
                FontSize = 11,
                Margin = new Thickness(4, 0, 0, 0)
            },
            JoinSlotRule.Absent => new TextBlock
            {
                Text = "✕",
                Foreground = MissingBrush,
                FontSize = 11,
                Margin = new Thickness(4, 0, 0, 0)
            },
            _ => new Rectangle
            {
                Width = 9,
                Height = 9,
                RadiusX = 2,
                RadiusY = 2,
                Fill = AnyBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            }
        };

        // Alongside a comparison the side is already named by the found chip above, so the rule only needs its symbol
        var chip = Chip(showLabel ? side : null, glyph, opacity);

        ToolTipService.SetToolTip(chip,
                                  rule switch
                                  {
                                      JoinSlotRule.Present => $"This join returns a row only when there is a {Label(isInner)} row",
                                      JoinSlotRule.Absent => $"This join returns a row only when there is no {Label(isInner)} row",
                                      _ => $"This join returns the row whether or not there is a {Label(isInner)} row"
                                  });

        return chip;
    }

    private static Border Chip(string? side, FrameworkElement glyph, double opacity)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };

        if (side is not null)
        {
            content.Children.Add(new TextBlock
            {
                Text = side,
                FontSize = 11,
                Foreground = ChipForeground,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        glyph.Margin = side is null ? new Thickness(0) : new Thickness(4, 0, 0, 0);

        content.Children.Add(glyph);

        return new Border
        {
            Child = content,
            Background = ChipBackground,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 0, 5, 1),
            Margin = new Thickness(0, 0, 4, 0),
            Opacity = opacity,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    /// <summary>
    /// Carries the comparison to its result, which the emit step then picks up
    /// </summary>
    private static StackPanel Outcome(bool isEmitted)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = "→",
            FontSize = 11,
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        });

        panel.Children.Add(new TextBlock
        {
            Text = isEmitted ? "✓" : "✕",
            Foreground = isEmitted ? FoundBrush : MissingBrush,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        ToolTipService.SetToolTip(panel,
                                  isEmitted
                                      ? "What was found satisfies the join, so a row is sent to the output"
                                      : "What was found does not satisfy the join, so nothing is sent to the output");

        return panel;
    }

    private static string Label(bool isInner) => isInner ? "inner" : "outer";
}
