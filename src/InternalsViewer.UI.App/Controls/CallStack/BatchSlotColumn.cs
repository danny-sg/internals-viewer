using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinUI.TableView;

namespace InternalsViewer.UI.App.Controls.CallStack;

internal sealed class BatchSlotColumn(BatchesViewModel batches, int ordinal) : TableViewColumn
{
    private static SolidColorBrush UnselectedBrush { get; } = new(Windows.UI.Color.FromArgb(48, 128, 128, 128));

    private static SolidColorBrush RemovedBrush { get; } = new(Windows.UI.Color.FromArgb(40, 196, 43, 28));

    private static SolidColorBrush PureBrush { get; } = new(Windows.UI.Color.FromArgb(28, 29, 158, 117));

    private static SolidColorBrush TransparentBrush { get; } = new(Colors.Transparent);

    private static SolidColorBrush DimTextBrush { get; } = new(Windows.UI.Color.FromArgb(110, 128, 128, 128));

    private static FontFamily Monospace { get; } = new("Cascadia Mono, Consolas, Courier New");

    public override FrameworkElement GenerateElement(TableViewCell cell, object? dataItem)
    {
        var host = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new TextBlock
            {
                FontFamily = Monospace,
                FontSize = 12,
                Padding = new Thickness(8, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
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
        host.Tag = dataItem as BatchSlotRow;

        if (host.Content is not TextBlock text || dataItem is not BatchSlotRow row)
        {
            cell.Background = TransparentBrush;

            return;
        }

        var isPure = BatchesViewModel.IsPure(row, ordinal);

        cell.Background = row.IsRemoved ? RemovedBrush : !row.IsSelected ? UnselectedBrush : isPure ? PureBrush : TransparentBrush;

        var value = batches.GetSlotText(row, ordinal);

        if (text.Text != value)
        {
            text.Text = value;
        }

        text.ClearValue(TextBlock.ForegroundProperty);

        if (isPure && row.RowIndex != 0)
        {
            text.Foreground = DimTextBrush;
        }
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is ContentControl { Tag: BatchSlotRow row })
        {
            batches.SelectSlot(row, ordinal);
        }
    }
}
