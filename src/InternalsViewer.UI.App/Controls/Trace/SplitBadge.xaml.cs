using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class SplitBadge : UserControl
{
    private const double LightenFraction = 0.62;

    private const double DarkTextThreshold = 0.58;

    private static readonly Color DarkText = Color.FromArgb(255, 0x33, 0x33, 0x33);

    public static readonly DependencyProperty BadgeNameProperty =
        DependencyProperty.Register(nameof(BadgeName), typeof(string), typeof(SplitBadge),
            new PropertyMetadata(string.Empty, OnBadgeChanged));

    public static readonly DependencyProperty BadgeValueProperty =
        DependencyProperty.Register(nameof(BadgeValue), typeof(object), typeof(SplitBadge),
            new PropertyMetadata(null, OnBadgeChanged));

    public static readonly DependencyProperty BadgeColourProperty =
        DependencyProperty.Register(nameof(BadgeColour), typeof(Color), typeof(SplitBadge),
            new PropertyMetadata(Color.FromArgb(255, 0x5A, 0x5A, 0x5A), OnBadgeChanged));

    public SplitBadge()
    {
        InitializeComponent();

        Apply();
    }

    public string BadgeName
    {
        get => (string)GetValue(BadgeNameProperty);
        set => SetValue(BadgeNameProperty, value);
    }

    public object? BadgeValue
    {
        get => GetValue(BadgeValueProperty);
        set => SetValue(BadgeValueProperty, value);
    }

    private string ValueLabel => BadgeValue?.ToString() ?? string.Empty;

    public Color BadgeColour
    {
        get => (Color)GetValue(BadgeColourProperty);
        set => SetValue(BadgeColourProperty, value);
    }

    /// <summary>
    /// Relative luminance, which decides whether text over the fill reads better dark or light
    /// </summary>
    private static double Luminance(Color colour)
        => ((0.2126 * Channel(colour.R)) + (0.7152 * Channel(colour.G)) + (0.0722 * Channel(colour.B)));

    private static double Channel(byte value)
    {
        var channel = value / 255D;

        return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static Color Lighten(Color colour)
        => Color.FromArgb(colour.A,
                          (byte)(colour.R + ((255 - colour.R) * LightenFraction)),
                          (byte)(colour.G + ((255 - colour.G) * LightenFraction)),
                          (byte)(colour.B + ((255 - colour.B) * LightenFraction)));

    private static SolidColorBrush TextOver(Color background)
        => new(Luminance(background) > DarkTextThreshold ? DarkText : Colors.White);

    private void Apply()
    {
        var value = Lighten(BadgeColour);

        NamePart.Background = new SolidColorBrush(BadgeColour);

        ValuePart.Background = new SolidColorBrush(value);

        NameText.Text = BadgeName;

        NameText.Foreground = TextOver(BadgeColour);

        ValueText.Text = ValueLabel;

        ValueText.Foreground = TextOver(value);

        ValuePart.Visibility = string.IsNullOrEmpty(ValueLabel) ? Visibility.Collapsed : Visibility.Visible;

        NamePart.CornerRadius = string.IsNullOrEmpty(ValueLabel) ? new CornerRadius(4) : new CornerRadius(4, 0, 0, 4);
    }

    private static void OnBadgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SplitBadge)d).Apply();
}
