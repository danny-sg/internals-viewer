using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Columns;

public sealed class ColumnHeaderPanel : Panel
{
    public static readonly DependencyProperty ColumnSpacingProperty =
        DependencyProperty.Register(nameof(ColumnSpacing),
                                    typeof(double),
                                    typeof(ColumnHeaderPanel),
                                    new PropertyMetadata(0d, OnColumnSpacingChanged));

    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    internal double[] ColumnOffsets { get; private set; } = [];

    internal double[] ColumnWidths { get; private set; } = [];

    internal double ContentWidth { get; private set; }

    private HashSet<ColumnRow> Rows { get; } = [];

    internal void AddRow(ColumnRow row) => Rows.Add(row);

    internal void RemoveRow(ColumnRow row) => Rows.Remove(row);

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateColumns(availableSize.Width);

        var height = 0d;

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];

            child.Measure(new Size(ColumnWidths[index], availableSize.Height));

            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(double.IsInfinity(availableSize.Width) ? ContentWidth : availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateColumns(finalSize.Width);

        for (var index = 0; index < Children.Count; index++)
        {
            Children[index].Arrange(new Rect(ColumnOffsets[index], 0, ColumnWidths[index], finalSize.Height));
        }

        return finalSize;
    }

    private void UpdateColumns(double availableWidth)
    {
        var fixedWidth = 0d;

        var stars = 0d;

        var visibleCount = 0;

        foreach (var child in Children)
        {
            if (GetVisibleColumn(child) is not { } column)
            {
                continue;
            }

            var length = column.CurrentWidth;

            if (length.IsAbsolute)
            {
                fixedWidth += length.Value;
            }
            else
            {
                stars += length.Value;
            }

            visibleCount++;
        }

        var spacing = Math.Max(visibleCount - 1, 0) * ColumnSpacing;

        var starWidth = stars > 0 && !double.IsInfinity(availableWidth)
            ? Math.Max(availableWidth - fixedWidth - spacing, 0) / stars
            : 0;

        var isChanged = ColumnWidths.Length != Children.Count;

        if (isChanged)
        {
            ColumnWidths = new double[Children.Count];

            ColumnOffsets = new double[Children.Count];
        }

        var x = 0d;

        for (var index = 0; index < Children.Count; index++)
        {
            var column = GetVisibleColumn(Children[index]);

            var width = column is null ? 0 : GetWidth(column, starWidth);

            isChanged |= ColumnOffsets[index] != x || ColumnWidths[index] != width;

            ColumnOffsets[index] = x;

            ColumnWidths[index] = width;

            if (column is not null)
            {
                x += width + ColumnSpacing;
            }
        }

        ContentWidth = Math.Max(x - ColumnSpacing, 0);

        if (!isChanged)
        {
            return;
        }

        foreach (var row in Rows)
        {
            row.InvalidateMeasure();
        }
    }

    private static ColumnHeader? GetVisibleColumn(UIElement child)
        => child is ColumnHeader { Visibility: Visibility.Visible } column ? column : null;

    private static double GetWidth(ColumnHeader column, double starWidth)
    {
        var length = column.CurrentWidth;

        var width = length.IsAbsolute ? length.Value : starWidth * length.Value;

        return Math.Max(Math.Min(width, column.MaxWidth), column.MinWidth);
    }

    private static void OnColumnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ColumnHeaderPanel)d).InvalidateMeasure();
}
