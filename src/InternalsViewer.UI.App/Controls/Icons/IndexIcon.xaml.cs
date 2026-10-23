using System.Drawing;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Xaml.Controls;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace InternalsViewer.UI.App.Controls.Icons;

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

        Nodes =
        [
            RootNode,
            IntermediateLeftNode, IntermediateRightNode,
            LeafLeftNode, LeafMiddleNode, LeafRightNode
        ];

        ApplyColour();
    }

    private Rectangle[] Nodes { get; }

    private void ApplyColour()
    {
        IconHighlight.FillShapes(Nodes, Colour, IconSize);
    }

    private static void OnColourChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((IndexIcon)d).ApplyColour();
    }
}
