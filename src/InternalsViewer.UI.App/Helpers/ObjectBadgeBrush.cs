using System.Collections.Generic;
using System.Drawing;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Helpers;

public static class ObjectBadgeBrush
{
    private const double MinimumLightness = 24;

    private const double MaximumLightness = 56;

    private const double HueStep = 5;

    private static Dictionary<(ObjectStage Stage, int Index), SolidColorBrush> Brushes { get; } = [];

    public static SolidColorBrush Get(ObjectStage stage, int index, int count)
    {
        if (!Brushes.TryGetValue((stage, index), out var brush))
        {
            var colour = ColourHelpers.FromHex(stage.GetCategoryMetadata()?.ForegroundColor)?.ToColor() ?? Color.Gray;

            brush = new SolidColorBrush(ColourHelpers.GetShade(colour, index, count, MinimumLightness, MaximumLightness, HueStep)
                                                     .ToWindowsColor());

            Brushes[(stage, index)] = brush;
        }

        return brush;
    }
}
