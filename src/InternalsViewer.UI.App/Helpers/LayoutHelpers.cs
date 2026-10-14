using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers;

public static class LayoutHelpers
{
    public static T? FindParent<T>(DependencyObject? source) where T : class
    {
        var target = source;

        while (target != null && target is not T)
        {
            target = VisualTreeHelper.GetParent(target);
        }

        return target as T;
    }

    public static void SetExpanded(TreeViewNode node, bool expanded)
    {
        if (node.IsExpanded != expanded)
        {
            node.IsExpanded = expanded;
        }

        foreach (var child in node.Children)
        {
            SetExpanded(child, expanded);
        }
    }
}
