using System;
using System.Drawing;
using InternalsViewer.Internals.Engine.Address;
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

    private const double BadgeSpacing = 4;

    private const double BadgeFontSize = 9;

    private const double SwatchSize = 20;

    private const double MarkerSwatchSize = 8;

    private const double MarkerSwatchSpacing = 6;

    private const string SwatchText = "00";

    private static readonly CornerRadius MarkerSwatchCorner = new(2);

    private static readonly Thickness CellPadding = new(8, 0, 4, 0);

    private static readonly Thickness ClassMargin = new(0, 0, 4, 0);

    private static readonly Thickness BadgePadding = new(5, 1, 5, 2);

    private static readonly Thickness BadgeBorder = new(1);

    private static readonly CornerRadius BadgeCorner = new(3);

    private static readonly SolidColorBrush BadgeBackground = new(Microsoft.UI.Colors.White);

    private static readonly SolidColorBrush BadgeForeground = new(Microsoft.UI.Colors.Black);

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
        var block = new TextBlock { Padding = CellPadding, FontSize = fontSize };

        var text = value is PageAddress or RowIdentifier or ObjectValue or StringValue or CallValue or ActionLinks or BadgeText
                            or ColouredText or Models.SwatchText or ActionText
            ? value.ToString()
            : ResultRowConverter.FormatValue(value);

        HexText.Apply(block, text);

        block.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

        return block.DesiredSize.Width;
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
            textBlock.Inlines.Add(new Run { Text = className, Foreground = Resource("CppClassBrush") });
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
            || (previous.Suffix is null) != (actions.Suffix is null))
        {
            panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ActionSpacing };

            foreach (var _ in actions.Links)
            {
                var button = new HyperlinkButton { Style = (Style)Application.Current.Resources["ResultPointerStyle"] };

                button.Click += OnActionClick;

                panel.Children.Add(button);
            }

            if (actions.Suffix is not null)
            {
                panel.Children.Add(new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Resource("TextFillColorSecondaryBrush")
                });
            }

            host.Content = panel;
        }

        panel.Tag = actions;

        for (var index = 0; index < actions.Links.Count; index++)
        {
            if (panel.Children[index] is HyperlinkButton button)
            {
                SetAction(button, actions.Links[index]);
            }
        }

        if (actions.Suffix is { } suffix && panel.Children[^1] is TextBlock text)
        {
            text.Text = suffix;
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

            panel.Children.Add(new TextBlock { VerticalAlignment = VerticalAlignment.Center });

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
        button.Content = link.Glyph is { } glyph ? new FontIcon { Glyph = glyph, FontSize = ActionGlyphSize } : link.Text;

        ToolTipService.SetToolTip(button, link.Glyph is null ? null : link.Text);

        button.Tag = link;
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

    private static SolidColorBrush CreateBrush(Color color)
    {
        var uiColor = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);

        return new SolidColorBrush(uiColor);
    }
}
