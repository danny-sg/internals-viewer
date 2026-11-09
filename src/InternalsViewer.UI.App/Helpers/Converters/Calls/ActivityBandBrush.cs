using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers.Converters.Calls;

/// <summary>
/// Draws call frame activity band
/// </summary>
public static class ActivityBandBrush
{
    private const double Floor = 0.3;

    public static Brush Fill(object? content, bool isExpanded)
    {
        if (Frame(content) is not { Activity: not null } node || node.ActivityCounts.Length == 0)
        {
            return new SolidColorBrush(Colors.Transparent);
        }

        var peak = node.ActivityCounts.Max();

        var slices = isExpanded
            ? Layer(node, peak, new Color[node.ActivityCounts.Length])
            : Overlay(node, peak);

        return Build(slices);
    }

    public static Visibility Visibility(object? content)
        => Frame(content) is { Activity: not null } node && node.ActivityCounts.Any(count => count > 0)
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    public static string? ToolTip(object? content)
    {
        if (Frame(content) is not { HasCalls: true } node)
        {
            return null;
        }

        var total = node.ActivityCounts.Sum(count => (long)count);

        return total > node.CallCount ? $"{node.CallsLabel}\n{total:N0} Calls Including Children" : node.CallsLabel;
    }

    private static CallTreeNode? Frame(object? content) => (content as TreeViewNode)?.Content as CallTreeNode;

    private static Color[] Overlay(CallTreeNode node, int peak)
    {
        var slices = new Color[node.ActivityCounts.Length];

        Visit(node);

        return slices;

        void Visit(CallTreeNode frame)
        {
            if (frame.ActivityCounts.Length == slices.Length)
            {
                Layer(frame, peak, slices);
            }

            foreach (var child in frame.Activity?.Children ?? [])
            {
                Visit(child);
            }
        }
    }

    private static Color[] Layer(CallTreeNode frame, int peak, Color[] slices)
    {
        if (peak == 0)
        {
            return slices;
        }

        var colour = ParseColour(frame.CategoryColour);

        for (var bucket = 0; bucket < slices.Length; bucket++)
        {
            var count = frame.ActivityCounts[bucket];

            if (count == 0)
            {
                continue;
            }

            var alpha = Floor + (1 - Floor) * Math.Min(count, peak) / peak;

            slices[bucket] = Color.FromArgb((byte)Math.Round(alpha * 255), colour.R, colour.G, colour.B);
        }

        return slices;
    }

    private static LinearGradientBrush Build(IReadOnlyList<Color> slices)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };

        var buckets = slices.Count;

        var start = 0;

        while (start < buckets)
        {
            var slice = slices[start];

            var end = start + 1;

            while (end < buckets && slices[end] == slice)
            {
                end++;
            }

            brush.GradientStops.Add(new GradientStop { Color = slice, Offset = (double)start / buckets });
            brush.GradientStops.Add(new GradientStop { Color = slice, Offset = (double)end / buckets });

            start = end;
        }

        return brush;
    }

    private static Color ParseColour(string hex) => ColourHelpers.FromHex(hex) ?? Colors.Gray;
}
