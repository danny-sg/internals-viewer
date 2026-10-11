using System;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Columns;

public sealed class ColumnRow : Panel
{
    public ColumnRow()
    {
        Unloaded += ColumnRow_Unloaded;
    }

    private ColumnHeaderPanel? Header { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        Header ??= FindHeader();

        var widths = Header?.ColumnWidths ?? [];

        var height = 0d;

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];

            var width = index < widths.Length ? widths[index] : availableSize.Width;

            child.Measure(new Size(width, availableSize.Height));

            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(Header?.ContentWidth ?? 0, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var offsets = Header?.ColumnOffsets ?? [];

        var widths = Header?.ColumnWidths ?? [];

        for (var index = 0; index < Children.Count; index++)
        {
            var bounds = index < widths.Length
                ? new Rect(offsets[index], 0, widths[index], finalSize.Height)
                : new Rect(0, 0, 0, finalSize.Height);

            Children[index].Arrange(bounds);
        }

        return new Size(Header?.ContentWidth ?? finalSize.Width, finalSize.Height);
    }

    private ColumnHeaderPanel? FindHeader()
    {
        var header = this.FindAscendant<ItemsPresenter>()?.Header switch
        {
            ColumnHeaderPanel panel => panel,
            DependencyObject element => element.FindDescendant<ColumnHeaderPanel>(),
            _ => null
        };

        header?.AddRow(this);

        return header;
    }

    private void ColumnRow_Unloaded(object sender, RoutedEventArgs e)
    {
        Header?.RemoveRow(this);

        Header = null;
    }
}
