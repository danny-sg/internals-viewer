using System.Drawing;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Xaml.Controls;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace InternalsViewer.UI.App.Controls.Page;

/// <summary>
/// Page tab icon coloured by an allocation unit's display colour
/// </summary>
public sealed partial class PageIcon : UserControl
{
    private const double IconSize = 32;

    public static readonly DependencyProperty ColourProperty =
        DependencyProperty.Register(nameof(Colour),
                                    typeof(Color),
                                    typeof(PageIcon),
                                    new PropertyMetadata(Color.Gray, OnColourChanged));

    public Color Colour
    {
        get => (Color)GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public PageIcon()
    {
        InitializeComponent();

        Bands = [HeaderBand];

        ApplyColour();
    }

    private Rectangle[] Bands { get; }

    private void ApplyColour()
    {
        IconHighlight.FillShapes(Bands, Colour, IconSize);
    }

    private static void OnColourChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PageIcon)d).ApplyColour();
    }
}
