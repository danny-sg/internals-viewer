using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Helpers.Converters.Results;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.Results;

internal sealed class ResultCellColumn(int ordinal) : TableViewColumn
{
    private const double ActionSpacing = 12;

    private const double ActionGlyphSize = 12;

    private const double LinkInset = 5;

    private const double LabelSpacing = 3;

    private const string ListStart = "[";

    private const string ListSeparator = ", ";

    private const string ListEnd = "]";

    private const double BadgeSpacing = 4;

    private const double BadgeFontSize = 9;

    private const double ObjectBadgeSpacing = 6;

    private const string ObjectBadgeStyle = "ObjectBadgeStyle";

    private const string ObjectBadgeTextStyle = "ObjectBadgeTextStyle";

    private const double SwatchSize = 20;

    private const double MarkerSwatchSize = 8;

    private const double MarkerSwatchSpacing = 6;

    private const string SwatchText = "00";

    private static readonly CornerRadius MarkerSwatchCorner = new(2);

    private static readonly Thickness CellPadding = new(8, 0, 4, 0);

    private static readonly Thickness ChainedLinkMargin = new(-ActionSpacing, 0, 0, 0);

    private static readonly Thickness ActionPadding = new(CellPadding.Left - LinkInset, 0, 0, 0);

    private static readonly Thickness LabelMargin = new(LabelSpacing - ActionSpacing, 0, 0, 0);

    private static readonly Thickness ObjectBadgeMargin = new(ObjectBadgeSpacing - ActionSpacing, 0, 0, 0);

    private static readonly Thickness ClassMargin = new(0, 0, 4, 0);

    private static readonly Thickness BadgePadding = new(5, 1, 5, 2);

    private static readonly Thickness BadgeBorder = new(1);

    private static readonly CornerRadius BadgeCorner = new(3);

    private static readonly SolidColorBrush BadgeBackground = new(Microsoft.UI.Colors.White);

    private static readonly SolidColorBrush BadgeForeground = new(Microsoft.UI.Colors.Black);

    private static readonly SolidColorBrush ColouredBadgeForeground = new(Microsoft.UI.Colors.White);


    public int Ordinal { get; } = ordinal;

    public Color? BackgroundColour { get; init; }

    public ResultAlignment Alignment { get; init; } = ResultAlignment.Left;

    /// <summary>
    /// Raised when a page address or row identifier link is clicked
    /// </summary>
    public Action<PageAddressEventArgs>? PageClicked { get; init; }

    public Action<ObjectValue>? ObjectClicked { get; init; }

    public Action<CallValue>? CallClicked { get; init; }

    public Action<ResultCellHoverEventArgs>? Hovered { get; init; }

    private SolidColorBrush? BackgroundBrushField { get; set; }

    private SolidColorBrush? BackgroundBrush => BackgroundColour.HasValue
        ? BackgroundBrushField ??= CreateBrush(BackgroundColour.Value)
        : null;

    public static double MeasureWidth(object? value, double fontSize)
    {
        if (value is ActionLinks actions)
        {
            return MeasureActions(actions, fontSize);
        }

        var text = value is PageAddress or RowIdentifier or ObjectValue or StringValue or CallValue or BadgeText
                            or ColouredText or Models.SwatchText or ActionText
            ? value.ToString()
            : ResultRowConverter.FormatValue(value);

        return CellPadding.Left + MeasureText(text, fontSize) + CellPadding.Right;
    }

    public override FrameworkElement GenerateElement(TableViewCell cell, object? dataItem)
    {
        var host = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = ToHorizontalAlignment(Alignment),
            VerticalContentAlignment = VerticalAlignment.Center

        };

        if (BackgroundBrush is { } columnBrush)
        {
            cell.Background = columnBrush;
        }

        if (Hovered is not null)
        {
            host.PointerEntered += (_, _) => RaiseHover(host, true);

            host.PointerExited += (_, _) => RaiseHover(host, false);
        }

        Apply(host, cell, dataItem);

        return host;
    }

    public override void RefreshElement(TableViewCell cell, object? dataItem)
    {
        if (cell.Content is ContentControl host)
        {
            Apply(host, cell, dataItem);
        }
    }

    public override FrameworkElement GenerateEditingElement(TableViewCell cell, object? dataItem)
    {
        return GenerateElement(cell, dataItem);
    }

    protected override object PrepareCellForEdit(TableViewCell cell, RoutedEventArgs editingEventArgs)
    {
        return null!;
    }

    private void Apply(ContentControl host, TableViewCell cell, object? dataItem)
    {
        if (dataItem is not ResultRow<long> row || Ordinal < 0 || Ordinal >= row.FieldCount)
        {
            host.Content = null;
            SetNullBackground(cell, false);

            return;
        }

        var value = row[Ordinal];

        host.Tag = row;

        switch (value)
        {
            case PageAddress pageAddress:
                ApplyLink(host, pageAddress.ToString(), pageAddress, null);
                break;

            case RowIdentifier rowIdentifier:
                ApplyLink(host, rowIdentifier.ToString(), rowIdentifier.PageAddress, rowIdentifier.SlotId);
                break;

            case ObjectValue objectValue:
                ApplyObject(host, objectValue);
                break;

            case StringValue stringValue:
                ApplyString(host, stringValue);
                break;

            case CallValue callValue:
                ApplyCallLink(host, callValue);
                break;

            case ActionLinks actions:
                ApplyActions(host, actions);
                break;

            case ActionText actionText:
                ApplyActionText(host, actionText);
                break;

            case BadgeText badgeText:
                ApplyBadges(host, badgeText);
                break;

            case SwatchText swatchText:
                ApplySwatch(host, swatchText);
                break;

            case ColouredText colouredText:
                ApplyColoured(host, colouredText);
                break;

            default:
                ApplyText(host, ResultRowConverter.FormatValue(value));
                break;
        }

        SetNullBackground(cell, value is null);
    }

    private static void ApplyText(ContentControl host, string text)
    {
        if (host.Content is TextBlock textBlock)
        {
            if (textBlock.Text != text)
            {
                HexText.Apply(textBlock, text);
            }

            return;
        }

        var block = new TextBlock
        {
            Padding = CellPadding,
            VerticalAlignment = VerticalAlignment.Center
        };

        HexText.Apply(block, text);

        host.Content = block;
    }

    private static void ApplyString(ContentControl host, StringValue value)
    {
        if (host.Content is not TextBlock textBlock)
        {
            textBlock = new TextBlock
            {
                Padding = CellPadding,
                VerticalAlignment = VerticalAlignment.Center
            };

            host.Content = textBlock;
        }

        textBlock.Inlines.Clear();

        StringText.AddRuns(textBlock.Inlines, value);
    }

    private void ApplyObject(ContentControl host, ObjectValue value)
    {
        if (value.Origin is not null && ObjectClicked is not null)
        {
            ApplyObjectLink(host, value);

            return;
        }

        if (host.Content is not TextBlock textBlock)
        {
            textBlock = new TextBlock
            {
                Padding = CellPadding,
                VerticalAlignment = VerticalAlignment.Center
            };

            host.Content = textBlock;
        }

        textBlock.Inlines.Clear();

        if (value.ClassName is { } className)
        {
            textBlock.Inlines.Add(new Run { Text = className, Foreground = Resource(GetClassBrush(value)) });
            textBlock.Inlines.Add(new Run { Text = " " });
        }

        HexText.AddRuns(textBlock.Inlines, value.Text);
    }

    private void ApplyObjectLink(ContentControl host, ObjectValue value)
    {
        if (host.Content is not StackPanel { Children: [TextBlock classText, TextBlock linkText] })
        {
            classText = new TextBlock
            {
                Foreground = Resource("CppClassBrush"),
                Margin = ClassMargin,
                VerticalAlignment = VerticalAlignment.Center
            };

            linkText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

            HoverUnderline.Attach(linkText);

            host.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Padding = CellPadding,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { classText, linkText }
            };
        }

        classText.Text = value.ClassName ?? string.Empty;

        classText.Foreground = Resource(GetClassBrush(value));

        classText.Visibility = value.ClassName is null ? Visibility.Collapsed : Visibility.Visible;

        var link = new Hyperlink { UnderlineStyle = UnderlineStyle.None };

        HexText.AddRuns(link.Inlines, value.Text);

        ToolTipService.SetToolTip(link, "Go To Origin");

        link.Click += (_, _) => ObjectClicked?.Invoke(value);

        linkText.Inlines.Clear();

        linkText.Inlines.Add(link);
    }

    private void ApplyLink(ContentControl host, string text, PageAddress pageAddress, ushort? slot)
    {
        var target = new PageAddressEventArgs(pageAddress.FileId, pageAddress.PageId) { Slot = slot ?? 0 };

        if (host.Content is HyperlinkButton existing)
        {
            existing.Content = text;
            existing.Tag = target;

            return;
        }

        var button = new HyperlinkButton
        {
            Content = text,
            Tag = target,
            Style = (Style)Application.Current.Resources["ResultPointerStyle"]
        };

        ToolTipService.SetToolTip(button, "Open Page");

        button.Click += OnLinkClick;

        host.Content = button;
    }

    private void ApplyCallLink(ContentControl host, CallValue value)
    {
        if (host.Content is HyperlinkButton { Tag: CallValue } existing)
        {
            existing.Content = value.ToString();
            existing.Tag = value;

            return;
        }

        var button = new HyperlinkButton
        {
            Content = value.ToString(),
            Tag = value,
            Style = (Style)Application.Current.Resources["ResultPointerStyle"]
        };

        ToolTipService.SetToolTip(button, "Go To Call");

        button.Click += OnCallLinkClick;

        host.Content = button;
    }

    private static void ApplyActions(ContentControl host, ActionLinks actions)
    {
        if (host.Content is not StackPanel { Tag: ActionLinks previous } panel
            || previous.Links.Count != actions.Links.Count
            || previous.Badges.Count != actions.Badges.Count
            || (previous.Prefix is null) != (actions.Prefix is null)
            || (previous.Suffix is null) != (actions.Suffix is null)
            || (previous.Targets.Count > 0) != (actions.Targets.Count > 0)
            || (previous.ObjectBadges.Count > 0) != (actions.ObjectBadges.Count > 0)
            || previous.IsChained != actions.IsChained)
        {
            panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ActionSpacing, Padding = ActionPadding };

            if (actions.Prefix is not null)
            {
                panel.Children.Add(new HyperlinkButton
                {
                    Style = (Style)Application.Current.Resources["ResultPointerStyle"],
                    Foreground = Resource("TextFillColorPrimaryBrush"),
                    IsHitTestVisible = false,
                    IsTabStop = false
                });
            }

            for (var index = 0; index < actions.Links.Count; index++)
            {
                var button = new HyperlinkButton { Style = (Style)Application.Current.Resources["ResultPointerStyle"] };

                if (actions.IsChained && index > 0)
                {
                    button.Margin = ChainedLinkMargin;
                }

                button.Click += OnActionClick;

                panel.Children.Add(button);
            }

            foreach (var badge in actions.Badges)
            {
                panel.Children.Add(CreateBadge(badge));
            }

            var labelMargin = actions.Badges.Count == 0 ? LabelMargin : default;

            if (actions.Targets.Count > 0)
            {
                panel.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = labelMargin
                });
            }
            else if (actions.Suffix is not null)
            {
                var label = CreateSecondaryText(string.Empty);

                label.Margin = labelMargin;

                panel.Children.Add(label);
            }

            if (actions.ObjectBadges.Count > 0)
            {
                panel.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = BadgeSpacing,
                    Margin = ObjectBadgeMargin
                });
            }

            host.Content = panel;
        }

        panel.Tag = actions;

        var first = actions.Prefix is null ? 0 : 1;

        if (actions.Prefix is { } prefix && panel.Children[0] is HyperlinkButton leading)
        {
            SetLinkText(leading, prefix);
        }

        for (var index = 0; index < actions.Links.Count; index++)
        {
            if (panel.Children[first + index] is HyperlinkButton button)
            {
                SetAction(button, actions.Links[index]);
            }
        }

        for (var index = 0; index < actions.Badges.Count; index++)
        {
            if (panel.Children[first + actions.Links.Count + index] is Border { Child: TextBlock badge } border)
            {
                badge.Text = actions.Badges[index];

                SetBadgeColour(border, badge, actions.BadgeColour);
            }
        }

        var tail = actions.ObjectBadges.Count > 0 ? 2 : 1;

        if (actions.Targets.Count > 0 && panel.Children[^tail] is StackPanel targets)
        {
            SetTargets(targets, actions.Targets);
        }
        else if (actions.Suffix is { } suffix && panel.Children[^tail] is TextBlock text)
        {
            SetLabel(text, suffix);
        }

        if (actions.ObjectBadges.Count > 0 && panel.Children[^1] is StackPanel objects)
        {
            SetObjectBadges(objects, actions.ObjectBadges);
        }
    }

    private static void SetBadgeColour(Border border, TextBlock badge, Windows.UI.Color? colour)
    {
        var background = colour is { } found ? new SolidColorBrush(found) : BadgeBackground;

        border.Background = background;

        border.BorderBrush = background;

        badge.Foreground = colour is null ? BadgeForeground : ColouredBadgeForeground;
    }

    private static void SetObjectBadges(StackPanel panel, IReadOnlyList<ObjectBadge> badges)
    {
        if (panel.Tag is IReadOnlyList<ObjectBadge> current && current.SequenceEqual(badges))
        {
            return;
        }

        panel.Children.Clear();

        foreach (var badge in badges)
        {
            var border = new Border
            {
                Style = AppResources.Get<Style>(ObjectBadgeStyle),
                Background = ObjectBadgeBrush.Get(badge.Stage, badge.Index, badge.Count),
                Child = new TextBlock { Text = badge.Name, Style = AppResources.Get<Style>(ObjectBadgeTextStyle) }
            };

            ToolTipService.SetToolTip(border, badge.StageName);

            panel.Children.Add(border);
        }

        panel.Tag = badges;
    }

    private static void SetTargets(StackPanel panel, IReadOnlyList<ActionLink> targets)
    {
        panel.Children.Clear();

        var isList = targets.Count > 1;

        if (isList)
        {
            panel.Children.Add(CreateSecondaryText(ListStart));
        }

        for (var index = 0; index < Math.Min(targets.Count, PointerArrayText.MaximumShown); index++)
        {
            if (index > 0)
            {
                panel.Children.Add(CreateSecondaryText(ListSeparator));
            }

            var button = new HyperlinkButton { Style = (Style)Application.Current.Resources["DetailLinkStyle"] };

            button.Click += OnActionClick;

            SetAction(button, targets[index]);

            panel.Children.Add(button);
        }

        if (targets.Count > PointerArrayText.MaximumShown)
        {
            panel.Children.Add(CreateSecondaryText($"{ListSeparator}{PointerArrayText.GetMoreText(targets.Count)}"));
        }

        if (isList)
        {
            panel.Children.Add(CreateSecondaryText(ListEnd));
        }
    }

    private static TextBlock CreateSecondaryText(string text) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        TextLineBounds = TextLineBounds.TrimToCapHeight,
        Foreground = Resource("TextFillColorSecondaryBrush")
    };

    private static void SetLabel(TextBlock text, string label)
    {
        text.Inlines.Clear();

        if (TextLabel.GetString(label) is { } value)
        {
            StringText.AddRuns(text.Inlines, value);
        }
        else
        {
            text.Inlines.Add(new Run { Text = label });
        }
    }

    private static void ApplyActionText(ContentControl host, ActionText value)
    {
        if (host.Content is not StackPanel { Tag: ActionText } panel || panel.Children.Count != value.Actions.Count + 1)
        {
            panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = MarkerSwatchSpacing,
                Padding = CellPadding,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextLineBounds = TextLineBounds.TrimToCapHeight
            });

            foreach (var _ in value.Actions)
            {
                var button = new HyperlinkButton { Style = (Style)Application.Current.Resources["ResultPointerStyle"] };

                button.Click += OnActionClick;

                panel.Children.Add(button);
            }

            host.Content = panel;
        }

        panel.Tag = value;

        HexText.Apply((TextBlock)panel.Children[0], value.Text);

        for (var index = 0; index < value.Actions.Count; index++)
        {
            if (panel.Children[index + 1] is HyperlinkButton button)
            {
                SetAction(button, value.Actions[index]);
            }
        }
    }

    private static void SetAction(HyperlinkButton button, ActionLink link)
    {
        if (link.Glyph is { } glyph)
        {
            button.Content = new FontIcon { Glyph = glyph, FontSize = ActionGlyphSize };
        }
        else
        {
            SetLinkText(button, link.Text);
        }

        ToolTipService.SetToolTip(button, link.Glyph is null ? null : link.Text);

        button.Tag = link;
    }

    private static void SetLinkText(HyperlinkButton button, string text)
    {
        var block = button.Content as TextBlock ?? new TextBlock { TextLineBounds = TextLineBounds.TrimToCapHeight };

        HexText.Apply(block, text);

        button.Content = block;
    }

    private static void ApplyBadges(ContentControl host, BadgeText value)
    {
        if (host.Content is StackPanel { Tag: BadgeText current } && current == value)
        {
            return;
        }

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = BadgeSpacing,
            Padding = CellPadding,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = value
        };

        foreach (var badge in value.LeadingBadges)
        {
            panel.Children.Add(CreateBadge(badge));
        }

        panel.Children.Add(new TextBlock { Text = value.Text, VerticalAlignment = VerticalAlignment.Center });

        foreach (var badge in value.Badges)
        {
            panel.Children.Add(CreateBadge(badge));
        }

        if (value.Detail is { } detail)
        {
            panel.Children.Add(new TextBlock
            {
                Text = detail,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = AppResources.Get<Brush>("TextFillColorTertiaryBrush")
            });
        }

        host.Content = panel;
    }

    private static void ApplyColoured(ContentControl host, ColouredText value)
    {
        if (host.Content is StackPanel { Tag: ColouredText current } && current == value)
        {
            return;
        }

        var swatch = new Grid
        {
            Width = SwatchSize,
            Height = SwatchSize,
            Background = CreateBrush(value.Colour),
            VerticalAlignment = VerticalAlignment.Center
        };

        swatch.Children.Add(new TextBlock
        {
            Text = SwatchText,
            Style = AppResources.Get<Style>("HexTextBlockStyle"),
            Foreground = BadgeForeground,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = BadgeSpacing,
            Padding = CellPadding,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = value
        };

        panel.Children.Add(swatch);

        panel.Children.Add(new TextBlock { Text = value.Text, VerticalAlignment = VerticalAlignment.Center });

        host.Content = panel;
    }

    private static void ApplySwatch(ContentControl host, SwatchText value)
    {
        if (host.Content is StackPanel { Tag: SwatchText current } && current == value)
        {
            return;
        }

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = MarkerSwatchSpacing,
            Padding = CellPadding,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = value
        };

        panel.Children.Add(new Border
        {
            Width = MarkerSwatchSize,
            Height = MarkerSwatchSize,
            CornerRadius = MarkerSwatchCorner,
            Background = CreateBrush(value.Colour),
            VerticalAlignment = VerticalAlignment.Center
        });

        panel.Children.Add(new TextBlock { Text = value.Text, VerticalAlignment = VerticalAlignment.Center });

        host.Content = panel;
    }

    private void RaiseHover(ContentControl host, bool isOver)
    {
        if (host.Tag is ResultRow<long> row && Ordinal >= 0 && Ordinal < row.FieldCount)
        {
            Hovered?.Invoke(new ResultCellHoverEventArgs(row, Ordinal, row[Ordinal], isOver));
        }
    }

    private static Border CreateBadge(string text)
        => new()
        {
            CornerRadius = BadgeCorner,
            Padding = BadgePadding,
            BorderThickness = BadgeBorder,
            BorderBrush = BadgeBackground,
            Background = BadgeBackground,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = BadgeFontSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = BadgeForeground
            }
        };

    private static double MeasureActions(ActionLinks actions, double fontSize)
    {
        var widths = new List<double>();

        if (actions.Prefix is { } prefix)
        {
            widths.Add(MeasureText(prefix, fontSize) + 2 * LinkInset);
        }

        widths.AddRange(actions.Links.Select((l, i) => (l.Glyph is null ? MeasureText(l.Text, fontSize) : ActionGlyphSize)
                                                       + 2 * LinkInset
                                                       + (actions.IsChained && i > 0 ? ChainedLinkMargin.Left : 0)));

        widths.AddRange(actions.Badges.Select(b => MeasureBadge(b) + BadgeBorder.Left + BadgeBorder.Right));

        var label = actions.Targets.Count > 0 ? PointerArrayText.Get([.. actions.Targets.Select(t => t.Text)]) : actions.Suffix;

        if (label is not null)
        {
            widths.Add(MeasureText(label, fontSize) + (actions.Badges.Count == 0 ? LabelMargin.Left : 0));
        }

        if (actions.ObjectBadges.Count > 0)
        {
            widths.Add(actions.ObjectBadges.Sum(b => MeasureBadge(b.Name))
                       + BadgeSpacing * (actions.ObjectBadges.Count - 1)
                       + ObjectBadgeMargin.Left);
        }

        return ActionPadding.Left + widths.Sum() + ActionSpacing * Math.Max(0, widths.Count - 1) + CellPadding.Right;
    }

    private static double MeasureBadge(string text)
        => MeasureText(text, BadgeFontSize, FontWeights.SemiBold) + BadgePadding.Left + BadgePadding.Right;

    private static double MeasureText(string? text, double fontSize, Windows.UI.Text.FontWeight? weight = null)
    {
        var block = new TextBlock { FontSize = fontSize, FontWeight = weight ?? FontWeights.Normal };

        HexText.Apply(block, text);

        block.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

        return block.DesiredSize.Width;
    }

    private static void OnActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Tag: ActionLink action })
        {
            action.Invoke();
        }
    }

    private void OnCallLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Tag: CallValue value })
        {
            CallClicked?.Invoke(value);
        }
    }

    private void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Tag: PageAddressEventArgs target })
        {
            PageClicked?.Invoke(target);
        }
    }

    private void SetNullBackground(TableViewCell cell, bool isNull)
    {
        if (BackgroundColour.HasValue)
        {
            return;
        }

        cell.Background = isNull
            ? ResultRowBackgroundConverter.NullBrush
            : ResultRowBackgroundConverter.TransparentBrush;
    }

    private static HorizontalAlignment ToHorizontalAlignment(ResultAlignment alignment) => alignment switch
    {
        ResultAlignment.Center => HorizontalAlignment.Center,
        ResultAlignment.Right => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Left
    };

    private static Brush? Resource(string key)
        => Application.Current.Resources.TryGetValue(key, out var resource) ? resource as Brush : null;

    private static string GetClassBrush(ObjectValue value) => value.IsLocal ? "CppLocalBrush" : "CppClassBrush";

    private static SolidColorBrush CreateBrush(Color color)
    {
        var uiColor = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);

        return new SolidColorBrush(uiColor);
    }
}
