using Windows.UI;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers.Converters;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Results;

internal sealed class ResultCellColumn(int ordinal) : DataGridBoundColumn
{
    private static readonly SolidColorBrush NullBrush =
        new(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xE5));

    private static readonly SolidColorBrush TransparentBrush =
        new(Colors.Transparent);

    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var rawValue = dataItem is ResultRow row ? row[ordinal] : null;
        var text = ResultRowConverter.FormatValue(rawValue);

        return new Border
        {
            Background = rawValue is null ? NullBrush : TransparentBrush,
            Padding = new Thickness(4, 0, 4, 0),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        => GenerateElement(cell, dataItem);

    protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs editingEventArgs)
        => null!;
}