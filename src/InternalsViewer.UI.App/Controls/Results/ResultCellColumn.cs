using System;
using System.Drawing;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers.Converters.Results;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.Results;

internal sealed class ResultCellColumn(int ordinal) : TableViewColumn
{
    public Color? BackgroundColour { get; init; }

    public ResultAlignment Alignment { get; init; } = ResultAlignment.Left;

    /// <summary>
    /// Raised when a page address or row identifier link is clicked.
    /// </summary>
    public Action<PageAddressEventArgs>? PageClicked { get; init; }

    private SolidColorBrush? BackgroundBrushField { get; set; }

    private SolidColorBrush? BackgroundBrush => BackgroundColour.HasValue
        ? BackgroundBrushField ??= CreateBrush(BackgroundColour.Value)
        : null;

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
                textBlock.Text = text;
            }

            return;
        }

        host.Content = new TextBlock
        {
            Text = text,
            Padding = new Thickness(8, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
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

    public override FrameworkElement GenerateEditingElement(TableViewCell cell, object? dataItem)
    {
        return GenerateElement(cell, dataItem);
    }

    protected override object PrepareCellForEdit(TableViewCell cell, RoutedEventArgs editingEventArgs)
    {
        return null!;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var uiColor = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
        return new SolidColorBrush(uiColor);
    }
}
