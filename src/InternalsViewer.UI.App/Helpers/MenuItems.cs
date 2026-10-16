using System;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers;

public static class MenuItems
{
    public static MenuFlyoutItem Create(string text, Action click)
    {
        var item = new MenuFlyoutItem { Text = text };

        item.Click += (_, _) => click();

        return item;
    }

    public static ToggleMenuFlyoutItem Toggle(string text, bool isChecked, Action<bool> setChecked)
    {
        var item = new ToggleMenuFlyoutItem { Text = text, IsChecked = isChecked };

        item.Click += (sender, _) => setChecked(sender is ToggleMenuFlyoutItem { IsChecked: true });

        return item;
    }

    public static MenuFlyoutSubItem SubMenu(string text, params MenuFlyoutItemBase[] items)
    {
        var subMenu = new MenuFlyoutSubItem { Text = text };

        foreach (var item in items)
        {
            subMenu.Items.Add(item);
        }

        return subMenu;
    }
}
