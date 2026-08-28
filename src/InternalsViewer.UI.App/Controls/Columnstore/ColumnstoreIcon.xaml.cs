using System.Drawing;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Xaml.Controls;
using Rectangle = Microsoft.UI.Xaml.Shapes.Rectangle;

namespace InternalsViewer.UI.App.Controls.Columnstore;

/// <summary>
/// Columnstore tab icon coloured by an allocation unit's display colour
/// </summary>
public sealed partial class ColumnstoreIcon : UserControl
{
    private const double IconSize = 32;

    public static readonly DependencyProperty ColourProperty =
        DependencyProperty.Register(nameof(Colour),
                                    typeof(Color),
                                    typeof(ColumnstoreIcon),
                                    new PropertyMetadata(Color.Gray, OnColourChanged));

    public Color Colour
    {
        get => (Color)GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public ColumnstoreIcon()
    {
        InitializeComponent();

        Bars = [LeftBar, MiddleBar, RightBar];

        ApplyColour();
    }

    private Rectangle[] Bars { get; }

    private void ApplyColour()
    {
        IconHighlight.FillShapes(Bars, Colour, IconSize);
    }

    private static void OnColourChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ColumnstoreIcon)d).ApplyColour();
    }
}
