using System;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.UI.App.Models.Query.Trace.Batch;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.Trace.Batch;

/// <summary>
/// One vector of the batch shown as a column of normalized slot values
/// </summary>
internal sealed class BatchValueColumn(BatchColumnView column) : TableViewColumn
{
    private static SolidColorBrush UnselectedBrush { get; } = new(Windows.UI.Color.FromArgb(48, 128, 128, 128));

    private static SolidColorBrush PureBrush { get; } = new(Windows.UI.Color.FromArgb(28, 86, 156, 214));

    private static SolidColorBrush TransparentBrush { get; } = new(Colors.Transparent);

    private static FontFamily Monospace { get; } = new("Cascadia Mono, Consolas, Courier New");

    private static SolidColorBrush LinkBrush { get; } = new(Windows.UI.Color.FromArgb(255, 86, 156, 214));

    private static SolidColorBrush DimLinkBrush { get; } = new(Windows.UI.Color.FromArgb(110, 86, 156, 214));

    private static SolidColorBrush DimTextBrush { get; } = new(Windows.UI.Color.FromArgb(110, 128, 128, 128));

    public Action<BatchValueSelection>? SlotClicked { get; init; }

    public Action<int>? DeepDataClicked { get; init; }

    public override FrameworkElement GenerateElement(TableViewCell cell, object? dataItem)
    {
        var host = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        host.Tapped += OnTapped;

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
        => GenerateElement(cell, dataItem);

    protected override object PrepareCellForEdit(TableViewCell cell, RoutedEventArgs editingEventArgs) => null!;

    private void Apply(ContentControl host, TableViewCell cell, object? dataItem)
    {
        if (dataItem is not BatchRowView row || column.Ordinal >= row.Values.Length)
        {
            host.Content = null;

            host.Tag = null;

            cell.Background = TransparentBrush;

            return;
        }

        host.Tag = row.RowIndex;

        cell.Background = !row.IsSelected ? UnselectedBrush : column.IsPure ? PureBrush : TransparentBrush;

        var slot = row.Values[column.Ordinal];

        var text = $"0x{slot.Value:X16}";

        if (BatchValueDenormalizer.GetValueType(slot, column.Column) == BatchValueType.DeepDataReference)
        {
            ApplyLink(host, text, (int)(slot.Value >> 1) - 1);

            ApplyForeground(host);

            return;
        }

        ApplyText(host, text);

        ApplyForeground(host);
    }

    private static void ApplyText(ContentControl host, string text)
    {
        if (host.Content is TextBlock existing)
        {
            if (existing.Text != text)
            {
                existing.Text = text;
            }

            return;
        }

        host.Content = new TextBlock
        {
            Text = text,
            FontFamily = Monospace,
            FontSize = 12,
            Padding = new Thickness(8, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void ApplyForeground(ContentControl host)
    {
        switch (host.Content)
        {
            case TextBlock text:
                text.ClearValue(TextBlock.ForegroundProperty);

                if (!column.IsInScope)
                {
                    text.Foreground = DimTextBrush;
                }

                break;

            case HyperlinkButton { Content: TextBlock label }:
                label.Foreground = column.IsInScope ? LinkBrush : DimLinkBrush;

                break;
        }
    }

    private void ApplyLink(ContentControl host, string text, int index)
    {
        if (host.Content is HyperlinkButton { Content: TextBlock label } existing)
        {
            existing.Tag = index;

            if (label.Text != text)
            {
                label.Text = text;
            }

            return;
        }

        var button = new HyperlinkButton
        {
            Tag = index,
            Padding = new Thickness(8, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new TextBlock
            {
                Text = text,
                FontFamily = Monospace,
                FontSize = 12,
                Foreground = LinkBrush,
                TextDecorations = TextDecorations.Underline,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        button.Click += OnLinkClick;

        host.Content = button;
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is ContentControl { Tag: int rowIndex })
        {
            SlotClicked?.Invoke(new BatchValueSelection(rowIndex, column.Ordinal));
        }
    }

    private void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Tag: int index })
        {
            DeepDataClicked?.Invoke(index);
        }
    }
}
