using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace InternalsViewer.UI.App.Controls.Docking;

public static class TabCommands
{
    public static StackPanel Panel(double spacing, params UIElement[] commands)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Spacing = spacing
        };

        foreach (var command in commands)
        {
            panel.Children.Add(command);
        }

        return panel;
    }

    public static Button Button(UIElement content, string toolTip, RoutedEventHandler click)
    {
        var button = new Button
        {
            Style = AppResources.Get<Style>("TabCommandButtonStyle"),
            Content = content,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        ToolTipService.SetToolTip(button, toolTip);

        button.Click += click;

        return button;
    }

    public static ToggleButton Toggle(UIElement content, bool isChecked, string toolTip, RoutedEventHandler click)
    {
        var toggle = new ToggleButton
        {
            Style = AppResources.Get<Style>("TabCommandToggleStyle"),
            Content = content,
            IsChecked = isChecked,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        ToolTipService.SetToolTip(toggle, toolTip);

        toggle.Click += click;

        return toggle;
    }

    public static TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    public static FontIcon Glyph(string glyph) => new() { Glyph = glyph, FontSize = 12 };

    public static bool IsChecked(object sender) => sender is ToggleButton { IsChecked: true };
}
