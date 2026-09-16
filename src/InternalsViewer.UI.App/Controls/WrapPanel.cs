using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls;

/// <summary>
/// Lays children left to right, wrapping to a new line when the available width runs out
/// </summary>
public sealed class WrapPanel : Panel
{
    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register(nameof(HorizontalSpacing),
                                    typeof(double),
                                    typeof(WrapPanel),
                                    new PropertyMetadata(0d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register(nameof(VerticalSpacing),
                                    typeof(double),
                                    typeof(WrapPanel),
                                    new PropertyMetadata(0d, OnLayoutPropertyChanged));

    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var maxWidth = availableSize.Width;

        var lineWidth = 0d;

        var lineHeight = 0d;

        var totalWidth = 0d;

        var totalHeight = 0d;

        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var size = child.DesiredSize;

            if (lineWidth > 0 && lineWidth + HorizontalSpacing + size.Width > maxWidth)
            {
                totalWidth = Math.Max(totalWidth, lineWidth);

                totalHeight += lineHeight + VerticalSpacing;

                lineWidth = size.Width;

                lineHeight = size.Height;

                continue;
            }

            lineWidth += (lineWidth > 0 ? HorizontalSpacing : 0) + size.Width;

            lineHeight = Math.Max(lineHeight, size.Height);
        }

        totalWidth = Math.Max(totalWidth, lineWidth);

        totalHeight += lineHeight;

        return new Size(double.IsInfinity(maxWidth) ? totalWidth : Math.Min(totalWidth, maxWidth), totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0d;

        var y = 0d;

        var lineHeight = 0d;

        foreach (var child in Children)
        {
            var size = child.DesiredSize;

            if (x > 0 && x + HorizontalSpacing + size.Width > finalSize.Width)
            {
                x = 0;

                y += lineHeight + VerticalSpacing;

                lineHeight = 0;
            }

            if (x > 0)
            {
                x += HorizontalSpacing;
            }

            child.Arrange(new Rect(x, y, size.Width, size.Height));

            x += size.Width;

            lineHeight = Math.Max(lineHeight, size.Height);
        }

        return finalSize;
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((WrapPanel)d).InvalidateMeasure();
}
