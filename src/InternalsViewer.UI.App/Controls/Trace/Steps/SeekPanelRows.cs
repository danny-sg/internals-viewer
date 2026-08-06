using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Trace.Steps;

internal static class SeekPanelRows
{
    public static TextBlock SectionHeader(string text, double topMargin)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, topMargin, 0, 4)
        };
    }

    public static Grid TitledRow(string title,
                                 double titleWidth,
                                 FrameworkElement content,
                                 bool semiBoldTitle = false,
                                 bool dimTitle = false)
    {
        var row = new Grid
        {
            Margin = new Thickness(0, 2, 0, 12)
        };

        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(titleWidth) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Top
        };

        if (semiBoldTitle)
        {
            titleBlock.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }

        if (dimTitle)
        {
            titleBlock.Opacity = 0.7;
        }

        row.Children.Add(titleBlock);

        content.HorizontalAlignment = HorizontalAlignment.Stretch;

        Grid.SetColumn(content, 1);

        row.Children.Add(content);

        return row;
    }
}
