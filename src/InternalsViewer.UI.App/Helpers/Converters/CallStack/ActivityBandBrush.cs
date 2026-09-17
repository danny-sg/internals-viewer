using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
using InternalsViewer.Query.CallStack;
using InternalsViewer.UI.App.Views.Query.Tabs.CallStack;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers.Converters.CallStack;

/// <summary>
/// Draws call frame activity band
/// </summary>
public static class ActivityBandBrush
{
    private const double Floor = 0.3;

    private const string MarkerColour = "#4CA3E0";

    public static Brush Fill(object? content, bool isExpanded)
    {
        if (Operator(content) is { Span: { } span } row)
        {
            return OperatorBrush(row, span);
        }

        if (Frame(content) is not { Activity: { } band } node || node.ActivityCounts.Length == 0)
        {
            return new SolidColorBrush(Colors.Transparent);
        }

        var peak = node.ActivityCounts.Max();

        var slices = isExpanded || content is not TreeViewNode treeNode
            ? Layer(node, peak, new Color[node.ActivityCounts.Length])
            : Overlay(treeNode, peak);

        foreach (var bucket in band.Markers)
        {
            if (bucket >= 0 && bucket < slices.Length)
            {
                slices[bucket] = ParseColour(MarkerColour);
            }
        }

        return Build(slices);
    }

    public static Visibility Visibility(object? content)
        => Operator(content) is { Span: not null }
           || (Frame(content) is { Activity: not null } node && node.ActivityCounts.Any(count => count > 0))
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    private static CallStackNode? Frame(object? content) => (content as TreeViewNode)?.Content as CallStackNode;

    private static OperatorRow? Operator(object? content) => (content as TreeViewNode)?.Content as OperatorRow;

    private static LinearGradientBrush OperatorBrush(OperatorRow row, ActivitySpan span)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };

        var colour = OperatorCategoryToBrushConverter.ColourFor(row.Operator.Category);

        brush.GradientStops.Add(new GradientStop { Color = Colors.Transparent, Offset = span.Start });
        brush.GradientStops.Add(new GradientStop { Color = colour, Offset = span.Start });
        brush.GradientStops.Add(new GradientStop { Color = colour, Offset = span.End });
        brush.GradientStops.Add(new GradientStop { Color = Colors.Transparent, Offset = span.End });

        return brush;
    }

    private static Color[] Overlay(TreeViewNode treeNode, int peak)
    {
        var slices = new Color[Frame(treeNode)!.ActivityCounts.Length];

        Visit(treeNode);

        return slices;

        void Visit(TreeViewNode current)
        {
            if (current.Content is CallStackNode frame && frame.ActivityCounts.Length == slices.Length)
            {
                Layer(frame, peak, slices);
            }

            foreach (var child in current.Children)
            {
                Visit(child);
            }
        }
    }

    private static Color[] Layer(CallStackNode frame, int peak, Color[] slices)
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

    private static Color ParseColour(string hex)
    {
        var digits = hex.TrimStart('#');

        if (digits.Length == 6 && uint.TryParse(digits, NumberStyles.HexNumber, null, out var rgb))
        {
            return Color.FromArgb(0xFF, (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        }

        return Colors.Gray;
    }
}
