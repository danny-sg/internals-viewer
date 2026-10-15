using System;
using InternalsViewer.UI.App.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Docking;

public static class TabCommands
{
    private const string ChevronGlyph = "\uE70D";

    private const double ChevronSize = 8;

    private const double MenuSpacing = 6;

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

    public static Button Menu(string text, string toolTip, MenuFlyout flyout)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = MenuSpacing };

        content.Children.Add(Label(text));

        content.Children.Add(new FontIcon { Glyph = ChevronGlyph, FontSize = ChevronSize, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button
        {
            Style = AppResources.Get<Style>("TabCommandButtonStyle"),
            Content = content,
            Flyout = flyout,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        ToolTipService.SetToolTip(button, toolTip);

        return button;
    }

    public static Button ToggleSplitButton(string toolTip, string prefix, int selectedIndex, Action<int> selectionChanged, params string[] options)
    {
        var label = Label(Prefixed(prefix, options[selectedIndex]));

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = MenuSpacing };

        content.Children.Add(label);

        content.Children.Add(new FontIcon { Glyph = ChevronGlyph, FontSize = ChevronSize, VerticalAlignment = VerticalAlignment.Center });

        var flyout = new MenuFlyout();

        var groupName = Guid.NewGuid().ToString();

        for (var index = 0; index < options.Length; index++)
        {
            var option = index;

            var item = new RadioMenuFlyoutItem { Text = options[option], GroupName = groupName, IsChecked = option == selectedIndex };

            item.Click += (_, _) =>
            {
                label.Text = Prefixed(prefix, options[option]);

                selectionChanged(option);
            };

            flyout.Items.Add(item);
        }

        var button = new Button
        {
            Style = AppResources.Get<Style>("TabCommandButtonStyle"),
            Content = content,
            Flyout = flyout,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        ToolTipService.SetToolTip(button, toolTip);

        return button;
    }

    public static TextBlock Label(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center };

    public static Border Separator() => new()
    {
        Width = 1,
        Margin = new Thickness(0, 4, 0, 4),
        VerticalAlignment = VerticalAlignment.Stretch,
        Background = AppResources.Get<Brush>("DividerStrokeColorDefaultBrush")
    };

    public static FontIcon Glyph(string glyph) => new() { Glyph = glyph, FontSize = 12 };

    public static bool IsChecked(object sender) => sender is ToggleButton { IsChecked: true };

    private static string Prefixed(string prefix, string option) => string.IsNullOrEmpty(prefix) ? option : $"{prefix} {option}";
}
