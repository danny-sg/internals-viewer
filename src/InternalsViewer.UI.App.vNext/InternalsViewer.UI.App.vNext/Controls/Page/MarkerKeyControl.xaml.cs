using System.Drawing;

namespace InternalsViewer.UI.App.vNext.Controls.Page;
public sealed partial class MarkerKeyControl
{
    public Color ForeColour
    {
        get { return (Color)GetValue(ForeColourProperty); }
        set { SetValue(ForeColourProperty, value); }
    }

    public static readonly DependencyProperty ForeColourProperty = DependencyProperty
        .Register(nameof(ForeColour),
            typeof(Color),
            typeof(MarkerKeyControl),
            new PropertyMetadata(default, null));

    public Color BackgroundColour
    {
        get { return (Color)GetValue(BackgroundColourProperty); }
        set { SetValue(BackgroundColourProperty, value); }
    }

    public static readonly DependencyProperty BackgroundColourProperty = DependencyProperty
        .Register(nameof(BackgroundColour),
            typeof(Color),
            typeof(MarkerKeyControl),
            new PropertyMetadata(default, null));

    public MarkerKeyControl()
    {
        InitializeComponent();
    }
}
