using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers;

public static class LayoutHelpers
{
    public static T? FindParent<T>(DependencyObject? source) where T : DependencyObject
    {
        var target = source;

        while (target != null && target is not T)
        {
            target = VisualTreeHelper.GetParent(target);
        }

        return target as T;
    }
}