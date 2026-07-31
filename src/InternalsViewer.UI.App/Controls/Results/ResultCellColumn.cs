using System;
using System.Drawing;
using CommunityToolkit.WinUI.UI.Controls;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers.Converters.Results;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Results;

internal sealed class ResultCellColumn(int ordinal) : DataGridBoundColumn
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

    protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
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

        host.DataContextChanged += (sender, args) => Apply((ContentControl)sender, cell, args.NewValue);

        Apply(host, cell, dataItem);

        return host;
    }

    private void Apply(ContentControl host, DataGridCell cell, object? dataItem)
    {
        if (dataItem is not ResultRow<long> row || ordinal < 0 || ordinal >= row.FieldCount)
        {
            host.Content = null;
            SetNullBackground(cell, false);

            return;
        }

        var value = row[ordinal];

        host.Content = value switch
        {
            PageAddress pageAddress => CreateLink(pageAddress.ToString(), pageAddress, null),
            RowIdentifier rowIdentifier => CreateLink(rowIdentifier.ToString(),
                rowIdentifier.PageAddress,
                rowIdentifier.SlotId),
            _ => CreateText(ResultRowConverter.FormatValue(value))
        };

        SetNullBackground(cell, value is null);
    }

    private static TextBlock CreateText(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Padding = new Thickness(4),
        VerticalAlignment = VerticalAlignment.Center
    };

    private HyperlinkButton CreateLink(string text, PageAddress pageAddress, ushort? slot)
    {
        var button = new HyperlinkButton
        {
            Content = text,
            Style = (Style)Application.Current.Resources["ResultPointerStyle"]
        };

        ToolTipService.SetToolTip(button, "Open Page");

        button.Click += (_, _) => PageClicked?.Invoke(
            new PageAddressEventArgs(pageAddress.FileId, pageAddress.PageId) { Slot = slot ?? 0 });

        return button;
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


    private static HorizontalAlignment ToHorizontalAlignment(ResultAlignment alignment) => alignment switch
    {
        ResultAlignment.Center => HorizontalAlignment.Center,
        ResultAlignment.Right => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Left
    };

    protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
    {
        return GenerateElement(cell, dataItem);
    }

    protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs editingEventArgs)
    {
        return null!;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var uiColor = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
        return new SolidColorBrush(uiColor);
    }
}