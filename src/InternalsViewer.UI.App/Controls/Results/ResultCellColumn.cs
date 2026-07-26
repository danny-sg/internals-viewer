
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers.Converters;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Drawing;

namespace InternalsViewer.UI.App.Controls.Results;

internal sealed class ResultCellColumn(int ordinal) : DataGridBoundColumn
{
    public Color? BackgroundColour { get; init; }

    private SolidColorBrush? _backgroundBrush;

    /// <summary>
    /// The fixed column background, if it has one, created once for the column rather than per cell
    /// </summary>
    private SolidColorBrush? BackgroundBrush => BackgroundColour.HasValue
        ? _backgroundBrush ??= CreateBrush(BackgroundColour.Value)
        : null;

    /// <remarks>
    /// Values are pushed in from <see cref="FrameworkElement.DataContextChanged"/> rather than bound. A cell is built
    /// for every row in the viewport and again on every recycle, and a binding per cell costs far more than the assignment
    /// it performs. <see cref="ResultRow{T}"/> raises no change notifications, so a binding would never re-evaluate on
    /// anything but a DataContext change anyway.
    /// </remarks>
    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
    {
        var textBlock = new TextBlock
        {
            FontSize = 11,
            Padding = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (BackgroundBrush is { } columnBrush)
        {
            cell.Background = columnBrush;
        }

        textBlock.DataContextChanged += (sender, args) => Apply((TextBlock)sender, cell, args.NewValue);

        Apply(textBlock, cell, dataItem);

        return textBlock;
    }

    private void Apply(TextBlock textBlock, DataGridCell cell, object? dataItem)
    {
        if (dataItem is not ResultRow<long> row || ordinal < 0 || ordinal >= row.FieldCount)
        {
            textBlock.Text = string.Empty;

            SetNullBackground(cell, false);

            return;
        }

        var value = row[ordinal];

        textBlock.Text = ResultRowConverter.FormatValue(value);

        SetNullBackground(cell, value is null);
    }

    private void SetNullBackground(DataGridCell cell, bool isNull)
    {
        if (BackgroundColour.HasValue)
        {
            return;
        }

        cell.Background = isNull
            ? ResultRowBackgroundConverter.NullBrush
            : ResultRowBackgroundConverter.TransparentBrush;
    }

    protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
    {
        return GenerateElement(cell, dataItem);
    }

    protected override object PrepareCellForEdit(
        FrameworkElement editingElement,
        RoutedEventArgs editingEventArgs)
    {
        return null!;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var uiColor = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
        return new SolidColorBrush(uiColor);
    }
}
