using System;
using InternalsViewer.UI.App.ViewModels.Docking;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Docking;

internal static class DockDropZones
{
    private const double EdgeFraction = 0.25;

    public static DropZone GetZone(Point point, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return DropZone.Center;
        }

        var left = point.X / width;

        var right = 1 - left;

        var top = point.Y / height;

        var bottom = 1 - top;

        var min = Math.Min(Math.Min(left, right), Math.Min(top, bottom));

        if (min > EdgeFraction)
        {
            return DropZone.Center;
        }

        if (min == left)
        {
            return DropZone.Left;
        }

        if (min == right)
        {
            return DropZone.Right;
        }

        return min == top ? DropZone.Top : DropZone.Bottom;
    }

    public static void Place(Border highlight, DropZone zone, double width, double height)
    {
        highlight.Visibility = Visibility.Visible;

        switch (zone)
        {
            case DropZone.Left:
                Set(highlight, HorizontalAlignment.Left, VerticalAlignment.Stretch, width / 2, double.NaN);
                break;

            case DropZone.Right:
                Set(highlight, HorizontalAlignment.Right, VerticalAlignment.Stretch, width / 2, double.NaN);
                break;

            case DropZone.Top:
                Set(highlight, HorizontalAlignment.Stretch, VerticalAlignment.Top, double.NaN, height / 2);
                break;

            case DropZone.Bottom:
                Set(highlight, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, height / 2);
                break;

            default:
                Set(highlight, HorizontalAlignment.Stretch, VerticalAlignment.Stretch, double.NaN, double.NaN);
                break;
        }
    }

    private static void Set(Border highlight, HorizontalAlignment horizontal, VerticalAlignment vertical, double width, double height)
    {
        highlight.HorizontalAlignment = horizontal;

        highlight.VerticalAlignment = vertical;

        highlight.Width = width;

        highlight.Height = height;
    }
}
