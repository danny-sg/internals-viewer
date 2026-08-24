using System;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace InternalsViewer.UI.App.Controls.Columnstore;

public static class CsIndexMenu
{
    /// <summary>
    /// Print modes the last argument takes, 0 to 2 all giving the parsed structure and 3 a raw memory dump
    /// </summary>
    private static readonly (int Mode, string Label)[] PrintModes =
    [
        (0, "Option 0 - Parsed structure"),
        (3, "Option 3 - Raw memory dump")
    ];

    public static MenuFlyout? Build(string label, Func<int, string?> buildCommand)
    {
        var submenu = new MenuFlyoutSubItem { Text = $"Copy DBCC CSINDEX command to clipboard ({label})" };

        foreach (var (mode, itemLabel) in PrintModes)
        {
            if (buildCommand(mode) is not { } command)
            {
                continue;
            }

            var item = new MenuFlyoutItem { Text = itemLabel };

            item.Click += (_, _) =>
            {
                var package = new DataPackage();

                package.SetText(command);

                Clipboard.SetContent(package);
            };

            submenu.Items.Add(item);
        }

        if (submenu.Items.Count == 0)
        {
            return null;
        }

        var flyout = new MenuFlyout();

        flyout.Items.Add(submenu);

        return flyout;
    }
}
