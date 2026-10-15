using System;
using System.Drawing;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers.Converters.Results;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.Results;

internal sealed class ResultCellColumn(int ordinal) : TableViewColumn
{
    private const double ActionSpacing = 12;

    private static readonly Thickness CellPadding = new(8, 0, 4, 0);

    private static readonly Thickness ClassMargin = new(0, 0, 4, 0);

    public Color? BackgroundColour { get; init; }

    public ResultAlignment Alignment { get; init; } = ResultAlignment.Left;

    /// <summary>
    /// Raised when a page address or row identifier link is clicked
    /// </summary>
    public Action<PageAddressEventArgs>? PageClicked { get; init; }

    public Action<ObjectValue>? ObjectClicked { get; init; }

    public Action<CallValue>? CallClicked { get; init; }

    public Action<RowBufferPage>? PagePointerClicked { get; init; }

    private SolidColorBrush? BackgroundBrushField { get; set; }

    private SolidColorBrush? BackgroundBrush => BackgroundColour.HasValue
        ? BackgroundBrushField ??= CreateBrush(BackgroundColour.Value)
        : null;

    public static double MeasureWidth(object? value, double fontSize)
    {
        var block = new TextBlock { Padding = CellPadding, FontSize = fontSize };

        var text = value is PageAddress or RowIdentifier or ObjectValue or StringValue or CallValue or RowBufferPage or ActionLinks
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
        if (dataItem is not ResultRow<long> row || ordinal < 0 || ordinal >= row.FieldCount)
        {
            host.Content = null;
            SetNullBackground(cell, false);

            return;
        }

        var value = row[ordinal];

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

            case RowBufferPage pointer when PagePointerClicked is not null:
                ApplyPagePointerLink(host, pointer);
                break;

            case ActionLinks actions:
                ApplyActions(host, actions);
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

    private void ApplyPagePointerLink(ContentControl host, RowBufferPage pointer)
    {
        if (host.Content is HyperlinkButton { Tag: RowBufferPage } existing)
        {
            existing.Content = pointer.ToString();
            existing.Tag = pointer;

            return;
        }

        var button = new HyperlinkButton
        {
            Content = pointer.ToString(),
            Tag = pointer,
            Style = (Style)Application.Current.Resources["ResultPointerStyle"]
        };

        ToolTipService.SetToolTip(button, "Open Page");

        button.Click += OnPagePointerClick;

        host.Content = button;
    }

    private static void ApplyActions(ContentControl host, ActionLinks actions)
    {
        if (host.Content is not StackPanel { Tag: ActionLinks } panel || panel.Children.Count != actions.Links.Count)
        {
            panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ActionSpacing };

            foreach (var _ in actions.Links)
            {
                var button = new HyperlinkButton { Style = (Style)Application.Current.Resources["ResultPointerStyle"] };

                button.Click += OnActionClick;

                panel.Children.Add(button);
            }

            host.Content = panel;
        }

        panel.Tag = actions;

        for (var index = 0; index < actions.Links.Count; index++)
        {
            if (panel.Children[index] is HyperlinkButton button)
            {
                button.Content = actions.Links[index].Text;

                button.Tag = actions.Links[index];
            }
        }
    }

    private static void OnActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Tag: ActionLink action })
        {
            action.Invoke();
        }
    }

    private void OnPagePointerClick(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Tag: RowBufferPage pointer })
        {
            PagePointerClicked?.Invoke(pointer);
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
