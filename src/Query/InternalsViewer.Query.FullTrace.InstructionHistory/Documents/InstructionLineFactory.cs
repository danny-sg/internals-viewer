using InternalsViewer.Query.FullTrace.InstructionHistory.Listing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Documents;

internal sealed partial class InstructionLineFactory : IElementFactory
{
    private const string CodeFontName = "Consolas";

    private const string SecondaryBrushKey = "TextFillColorSecondaryBrush";

    public InstructionLineFactory()
    {
        CodeFont = new FontFamily(CodeFontName);

        SecondaryBrush = Application.Current.Resources.TryGetValue(SecondaryBrushKey, out var value) ? value as Brush : null;
    }

    private FontFamily CodeFont { get; }

    private Brush? SecondaryBrush { get; }

    public UIElement GetElement(ElementFactoryGetArgs args)
    {
        if (args.Data is not InstructionLine line)
        {
            return new Grid();
        }

        var address = new TextBlock { FontFamily = CodeFont, Text = $"{line.Address:X16}" };

        if (SecondaryBrush is { } brush)
        {
            address.Foreground = brush;
        }

        var text = new TextBlock { FontFamily = CodeFont, Text = line.Text };

        var row = new Grid { Padding = new Thickness(8, 1, 8, 1), ColumnSpacing = 16 };

        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(text, 1);

        row.Children.Add(address);
        row.Children.Add(text);

        if (line.Explanation is { } explanation)
        {
            ToolTipService.SetToolTip(row, explanation);
        }

        return row;
    }

    public void RecycleElement(ElementFactoryRecycleArgs args)
    {
    }
}
