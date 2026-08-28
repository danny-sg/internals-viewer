using System.Drawing;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Xaml.Controls;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace InternalsViewer.UI.App.Controls.Index;

/// <summary>
/// Index tab icon coloured by an allocation unit's display colour
/// </summary>
public sealed partial class IndexIcon : UserControl
{
    private const double IconSize = 32;

    public static readonly DependencyProperty ColourProperty =
        DependencyProperty.Register(nameof(Colour),
                                    typeof(Color),
                                    typeof(IndexIcon),
                                    new PropertyMetadata(Color.Gray, OnColourChanged));

    public Color Colour
    {
        get => (Color)GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public IndexIcon()
    {
        InitializeComponent();

        Nodes = [RootNode, LeftNode, RightNode];

        ApplyColour();
    }

    private Rectangle[] Nodes { get; }

    private void ApplyColour()
    {
        foreach (var node in Nodes)
        {
            var start = Canvas.GetLeft(node) + Canvas.GetTop(node);

            var end = start + node.Width + node.Height;

            node.Fill = IconHighlight.CreateBrush(Colour, start / (IconSize * 2), end / (IconSize * 2));
        }
    }

    private static void OnColourChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((IndexIcon)d).ApplyColour();
    }
}
