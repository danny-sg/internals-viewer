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
}
