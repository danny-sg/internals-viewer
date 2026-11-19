using System.Collections.Generic;
using System.Linq;
using InternalsViewer.UI.App.Models;
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

    public static void AddCopy(MenuFlyout menu, object? cell)
    {
        var items = GetCopyItems(cell).ToList();

        if (items.Count == 0)
        {
            return;
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        foreach (var item in items)
        {
            menu.Items.Add(item);
        }
    }

    private static IEnumerable<MenuFlyoutItem> GetCopyItems(object? cell)
    {
        if (cell is ActionLinks links && GetDescription(links) is { Length: > 0 } description)
        {
            if ((links.Prefix ?? links.Links.FirstOrDefault()?.Text) is { Length: > 0 } address)
            {
                yield return Create("Copy Address", () => ClipboardText.Copy(address));
            }

            yield return Create("Copy Description", () => ClipboardText.Copy(description));

            yield break;
        }

        if (cell?.ToString() is { Length: > 0 } text)
        {
            yield return Create("Copy", () => ClipboardText.Copy(text));
        }
    }

    private static string? GetDescription(ActionLinks links)
        => links.Suffix ?? (links.Targets.Count > 0 ? PointerArrayText.Get([.. links.Targets.Select(t => t.Text)]) : null);
}
