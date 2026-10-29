using System;
using System.Linq;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Helpers;

public static class TraceLinkMenu
{
    private const string FirstSuffix = " First";

    private const string OffsetSeparator = " +0x";

    private static readonly string[] TypeQualifiers = ["const", "volatile", "struct", "class", "enum"];

    public static MenuFlyout CreateForPosition(ITraceNavigator navigator, TraceCallLink link) => Create(navigator, link, false, false);

    public static MenuFlyout CreateForFunction(ITraceNavigator navigator, TraceCallLink link, bool isFirst = false)
        => Create(navigator, link, isFirst, true);

    public static MenuFlyout CreateForAddress(ITraceNavigator navigator, ulong address)
    {
        var menu = new MenuFlyout();

        menu.Items.Add(MenuItems.Create("Find", () => navigator.FindReferences(address)));

        return menu;
    }

    public static void AddSearch(MenuFlyout menu, ITraceNavigator navigator, string? name)
    {
        if (GetSearchText(name) is { } text)
        {
            menu.Items.Add(MenuItems.Create("Search", () => navigator.Search(text)));
        }
    }

    public static string? GetSearchText(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var offset = name.IndexOf(OffsetSeparator, StringComparison.Ordinal);

        var words = (offset >= 0 ? name[..offset] : name).Replace('*', ' ')
                                                         .Replace('&', ' ')
                                                         .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                                         .Where(w => !TypeQualifiers.Contains(w));

        var text = string.Join(' ', words);

        return text.Length > 0 ? text : null;
    }

    public static void Show(MenuFlyout menu, UIElement target, ContextRequestedEventArgs e)
    {
        if (menu.Items.Count == 0)
        {
            return;
        }

        var options = new FlyoutShowOptions();

        if (e.TryGetPosition(target, out var position))
        {
            options.Position = position;
        }

        menu.ShowAt(target, options);

        e.Handled = true;
    }

    private static MenuFlyout Create(ITraceNavigator navigator, TraceCallLink link, bool isFirst, bool canFind)
    {
        var suffix = isFirst ? FirstSuffix : string.Empty;

        var menu = new MenuFlyout();

        if (link.Node is { } node && link.CallIndex >= 0)
        {
            menu.Items.Add(MenuItems.Create($"Zoom to{suffix}", () => navigator.ZoomToCall(node, link.CallIndex)));
        }

        menu.Items.Add(MenuItems.Create($"Call Stack{suffix}", () => navigator.ShowCallStack(link.Point)));

        if (canFind && link.Node is { } found && link.CallIndex >= 0)
        {
            menu.Items.Add(MenuItems.Create("Find", () => navigator.FindInCallTree(found, link.CallIndex)));
        }

        return menu;
    }
}
